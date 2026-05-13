#!/usr/bin/env python3
"""
ROS2 Drone Agent Node
Subscribes to /drone/pose (String) and /camera/image_raw (Image),
calls OpenAI with vision, and publishes commands to /motion_command (String).

Axis convention:
  forward  → +Z
  right    → +X
  up       → +Y
"""

import re
import json
import base64
import threading
from dataclasses import dataclass
from typing import Optional

import rclpy
from rclpy.node import Node
from rclpy.qos import QoSProfile, ReliabilityPolicy, HistoryPolicy

from std_msgs.msg import String
from sensor_msgs.msg import Image
from cv_bridge import CvBridge
import cv2
from openai import OpenAI

# ═══════════════════════════════════════════════════════════════
#  ✏️  EDIT HERE — Goal position the drone will navigate toward
# ═══════════════════════════════════════════════════════════════
# ========== SCENE 1 ================
# GOAL_X: float = 4.28
# GOAL_Y: float = 3.36
# GOAL_Z: float = 20.72
# ========== SCENE 2 ================
GOAL_X: float = 25.95
GOAL_Y: float = 2.3
GOAL_Z: float = 35.4
# ═══════════════════════════════════════════════════════════════

# ─────────────────────────────────────────────
# Other configuration
# ─────────────────────────────────────────────
OPENAI_MODEL   = "gpt-4o"
ARRIVAL_THRESH = 3    # metres – stop issuing commands inside this radius
MEMORY_SIZE    = 5      # how many past (step, command, reasoning) to keep

SYSTEM_PROMPT = """\
You are an autonomous drone navigation agent.

You receive:

Current Position: (x, y, z)

Goal Position (Aruco Tag): (gx, gy, gz)

Current Rotation (Drone initially faces +Z direction)

A Camera Image (USED ONLY for obstacle avoidance) - If the obstacle is at the below of the image(in the below 30% zone then you can go forward.)

Memory of recent actions

AXIS CONVENTION:

Forward = +Z

Backward = −Z

Right = +X

Left = −X

Up = +Y (increase height)

Down = −Y (decrease height)

Height from ground is represented by the +Y axis.

ALLOWED COMMANDS (choose EXACTLY ONE):
forward, backward, left, right, up, down, stop

OUTPUT FORMAT (STRICT):
Reply ONLY with valid JSON:

{
"command": "<one command>",
"reasoning": "<1-2 sentences explaining why>"
}

No markdown. No extra keys. No extra text.

CORE NAVIGATION RULE:
Navigation decisions MUST be made using the drone's position and the goal position.
The image must NOT be used for navigation — it is ONLY for detecting and avoiding nearby red obstacles.

INITIAL TAKEOFF RULE:
The first two movements must ALWAYS be:
up
up

This is considered the takeoff phase.

Do not perform any horizontal movement before completing these two ups.

GOAL REACH CONDITION:

Compute horizontal distance using ONLY X and Z:

distance = sqrt((x - gx)^2 + (z - gz)^2)

IF dz< abs(2) and dx < abs(2) and dy <abs(2):
You have reached the goal.

When the goal is reached:

Issue "stop" (to prevent overshooting).

Then go down twice on subsequent steps to land.

Do NOT move in X or Z anymore.

Never go down unless the goal has been reached.

OBSTACLE AVOIDANCE (IMAGE USE):

The image may contain:
Red Object = Obstacle

Use the image ONLY if:

The red object is close

AND located near the center of the image (direct collision risk)

If such an obstacle is detected:
Avoid using ONE of these:
up (preferred)
right
left

Do NOT avoid if the obstacle is far away or not blocking the path.

Obstacle avoidance should temporarily override navigation, but only when necessary.

DECISION ALGORITHM (PSEUDOCODE):

If the last action in memory is "stop":
Do nothing. Maintain position.

If fewer than two "up" actions have been executed:
command = up
(Complete mandatory takeoff.)


If distance ≤ 3:
command = stop

If goal reached previously and landing not finished:
command = down (until landed twice)

Otherwise, navigate toward the goal using position only. 

Before executing the chosen movement:
Check the image for a CLOSE red obstacle in front.

If obstacle detected:
Override with:
up (preferred) OR right OR left

Never use the image to decide direction toward the goal.
Image is ONLY for collision prevention.

IMPORTANT BEHAVIOR CONSTRAINTS:

Do NOT randomly explore.

Do NOT descend early.

Do NOT rely on visual detection of the Aruco tag.

Always trust positional data over vision.

Movement must be stable, minimal, and goal-directed.

After issuing stop, never resume navigation.

"""


# ─────────────────────────────────────────────
# Helpers
# ─────────────────────────────────────────────
@dataclass
class Pose:
    px: float = 0.0
    py: float = 0.0
    pz: float = 0.0
    rx: float = 0.0
    ry: float = 0.0
    rz: float = 0.0


@dataclass
class MemoryEntry:
    step: int
    command: str
    reasoning: str


def parse_pose(data: str) -> Pose:
    """Parse 'px:1.0,py:2.0,pz:3.0,rx:0,ry:0,rz:0' into a Pose."""
    p = Pose()
    for token in data.split(","):
        key, _, val = token.partition(":")
        key = key.strip()
        val = float(val.strip())
        if   key == "px": p.px = val
        elif key == "py": p.py = val
        elif key == "pz": p.pz = val
        elif key == "rx": p.rx = val
        elif key == "ry": p.ry = val
        elif key == "rz": p.rz = val
    return p


def encode_image(cv_img) -> str:
    """BGR OpenCV image → base64 JPEG string."""
    _, buf = cv2.imencode(".jpg", cv_img, [cv2.IMWRITE_JPEG_QUALITY, 75])
    return base64.b64encode(buf.tobytes()).decode("utf-8")


def distance(pose: Pose, goal) -> float:
    dx = pose.px - goal[0]
    dy = pose.py - goal[1]
    dz = pose.pz - goal[2]
    return (dx**2 + dy**2 + dz**2) ** 0.5


# ─────────────────────────────────────────────
# Node
# ─────────────────────────────────────────────
class DroneAgent(Node):

    def __init__(self):
        super().__init__("drone_agent")

        # ── goal & config (set the globals at the top of the file) ─────────────
        self._goal     = (GOAL_X, GOAL_Y, GOAL_Z)
        self._thresh   = ARRIVAL_THRESH
        self._mem_size = MEMORY_SIZE
        model          = OPENAI_MODEL

        # ── state ────────────────────────────────────────────────────────────
        self._bridge    = CvBridge()
        self._openai    = OpenAI()          # reads OPENAI_API_KEY from env
        self._model     = model

        self._latest_pose:  Optional[Pose]  = None
        self._latest_image                  = None   # OpenCV BGR
        self._memory:       list[MemoryEntry] = []
        self._step          = 0
        self._agent_busy    = False
        self._lock          = threading.Lock()

        # ── QoS ─────────────────────────────────────────────────────────────
        reliable_qos = QoSProfile(
            reliability=ReliabilityPolicy.RELIABLE,
            history=HistoryPolicy.KEEP_LAST,
            depth=10,
        )
        best_effort_qos = QoSProfile(
            reliability=ReliabilityPolicy.BEST_EFFORT,
            history=HistoryPolicy.KEEP_LAST,
            depth=1,
        )

        # ── subscribers ──────────────────────────────────────────────────────
        self.create_subscription(
            String,
            "/drone/pose",
            self._pose_callback,
            reliable_qos,
        )
        self.create_subscription(
            Image,
            "/camera/image_raw",
            self._image_callback,
            best_effort_qos,
        )

        # ── publisher ────────────────────────────────────────────────────────
        self._cmd_pub = self.create_publisher(String, "/motion_command", reliable_qos)

        # ── timer: decision loop at 1 Hz ─────────────────────────────────────
        self.create_timer(1.0, self._decision_loop)

        self.get_logger().info(
            f"DroneAgent started. Goal → X={GOAL_X}, Y={GOAL_Y}, Z={GOAL_Z} | "
            f"Arrival threshold: {ARRIVAL_THRESH} m"
        )

    # ─────────────────────────────────────────────────────────────────────────
    # Callbacks
    # ─────────────────────────────────────────────────────────────────────────
    def _pose_callback(self, msg: String):
        try:
            pose = parse_pose(msg.data)
            with self._lock:
                self._latest_pose = pose
        except Exception as e:
            self.get_logger().warn(f"Pose parse error: {e}")

    def _image_callback(self, msg: Image):
        try:
            cv_img = self._bridge.imgmsg_to_cv2(msg, desired_encoding="bgr8")
            with self._lock:
                self._latest_image = cv_img
        except Exception as e:
            self.get_logger().warn(f"Image convert error: {e}")

    # ─────────────────────────────────────────────────────────────────────────
    # Decision loop
    # ─────────────────────────────────────────────────────────────────────────
    def _decision_loop(self):
        """Called at 1 Hz. If not busy and we have fresh data, run the agent."""
        with self._lock:
            if self._agent_busy:
                return
            pose  = self._latest_pose
            image = self._latest_image

        if pose is None or image is None:
            self.get_logger().info("Waiting for pose + image data…")
            return

        dist = distance(pose, self._goal)
        if dist < self._thresh:
            self.get_logger().info(f"Goal reached! Distance={dist:.3f} m")
            return

        # Run the LLM call in a background thread so we don't block the executor
        with self._lock:
            self._agent_busy = True

        t = threading.Thread(
            target=self._run_agent,
            args=(pose, image.copy(), dist),
            daemon=True,
        )
        t.start()

    def _run_agent(self, pose: Pose, image, dist: float):
        try:
            self._step += 1
            step = self._step

            dx = self._goal[0] - pose.px
            dy = self._goal[1] - pose.py
            dz = self._goal[2] - pose.pz

            memory_block = self._build_memory_block()
            user_text = (
                f"Step {step}\n"
                f"Current position : px={pose.px:.4f}, py={pose.py:.4f}, pz={pose.pz:.4f}\n"
                f"Current rotation : rx={pose.rx:.4f}, ry={pose.ry:.4f}, rz={pose.rz:.4f}\n"
                f"Goal offset      : dx={dx:+.3f}, dy={dy:+.3f}, dz={dz:+.3f}\n"
                f"Distance to goal : {dist:.3f} m\n\n"
                f"{memory_block}\n\n"
                f"What single command should the drone execute next?"
            )

            image_b64 = encode_image(image)

            response = self._openai.responses.create(
                model=self._model,
                input=[
                    {
                        "role": "system",
                        "content": SYSTEM_PROMPT,
                    },
                    {
                        "role": "user",
                        "content": [
                            {"type": "input_text",  "text": user_text},
                            {
                                "type": "input_image",
                                "image_url": f"data:image/jpeg;base64,{image_b64}",
                            },
                        ],
                    },
                ],
            )

            raw = response.output_text.strip()
            # Strip accidental markdown fences
            raw = re.sub(r"^```[a-z]*\n?", "", raw)
            raw = re.sub(r"\n?```$", "", raw)

            parsed = json.loads(raw)
            command   = parsed["command"].strip().lower()
            reasoning = parsed.get("reasoning", "")

            valid = {"forward", "backward", "left", "right", "up", "down", "stop"}
            if command not in valid:
                self.get_logger().error(f"Invalid command from LLM: '{command}' – skipping.")
                return

            # Publish
            out_msg = String()
            out_msg.data = command
            self._cmd_pub.publish(out_msg)

            self.get_logger().info(
                f"[Step {step}] Goal offset: dx={dx:+.3f}, dy={dy:+.3f}, dz={dz:+.3f}  dist={dist:.2f}m → CMD={command} | {reasoning}"
            )

            # Update memory
            entry = MemoryEntry(step=step, command=command, reasoning=reasoning)
            self._memory.append(entry)
            if len(self._memory) > self._mem_size:
                self._memory.pop(0)

        except json.JSONDecodeError as e:
            self.get_logger().error(f"JSON parse error: {e} | raw={raw!r}")
        except Exception as e:
            self.get_logger().error(f"Agent error: {e}")
        finally:
            with self._lock:
                self._agent_busy = False

    # ─────────────────────────────────────────────────────────────────────────
    # Helpers
    # ─────────────────────────────────────────────────────────────────────────
    def _build_memory_block(self) -> str:
        if not self._memory:
            return "Memory: none"
        lines = ["Memory (most recent last):"]
        for m in self._memory:
            lines.append(f"  Step {m.step}: CMD={m.command} — {m.reasoning}")
        return "\n".join(lines)


# ─────────────────────────────────────────────
# Entry point
# ─────────────────────────────────────────────
def main(args=None):
    rclpy.init(args=args)
    node = DroneAgent()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    main()