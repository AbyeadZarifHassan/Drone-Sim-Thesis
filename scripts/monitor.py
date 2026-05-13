#!/usr/bin/env python3
"""
Drone Monitor Node
- Monitors all Unity-published topics for activity
- Fixes 180-degree rotated camera image → republishes to /camera/image_raw
- Prints live pose info to terminal
- Tkinter GUI dashboard
"""

import rclpy
from rclpy.node import Node
from rclpy.qos import QoSProfile, ReliabilityPolicy, HistoryPolicy

from sensor_msgs.msg import Image, Imu
from std_msgs.msg import String
from geometry_msgs.msg import Twist
from tf2_msgs.msg import TFMessage

import tkinter as tk
from tkinter import ttk
import threading
import queue
import time
from datetime import datetime
import numpy as np


# ─────────────────────────────────────────────────────────────────────────────
# ROS2 Node
# ─────────────────────────────────────────────────────────────────────────────
class DroneMonitorNode(Node):

    TOPICS = {
        "drone/image_raw": ("Image",     "pub"),
        "/drone/pose":     ("String",    "pub"),
        "drone/imu":       ("Imu",       "pub"),
        "cmd_vel":         ("Twist",     "sub"),
        "/tf":             ("TFMessage", "sub"),
    }

    def __init__(self):
        super().__init__("drone_monitor")

        # ── Shared state (write: ROS thread, read: GUI thread) ────────────────
        self._lock = threading.Lock()
        self.last_recv: dict[str, float] = {t: 0.0 for t in self.TOPICS}
        self.msg_count: dict[str, int]   = {t: 0   for t in self.TOPICS}
        self.pose_data = dict(px=0.0, py=0.0, pz=0.0, rx=0.0, ry=0.0, rz=0.0)
        self.imu_data  = dict(ax=0.0, ay=0.0, az=0.0)

        # ── Image work queue ──────────────────────────────────────────────────
        # ROS callback only enqueues; a dedicated worker thread does the numpy
        # flip and publishes. This prevents any cross-thread OpenCV/cv_bridge
        # context issues that cause segfaults.
        self._img_q: queue.Queue = queue.Queue(maxsize=2)
        threading.Thread(target=self._image_worker, daemon=True).start()

        # ── QoS matching Unity's BEST_EFFORT publishers ───────────────────────
        qos = QoSProfile(
            reliability=ReliabilityPolicy.BEST_EFFORT,
            history=HistoryPolicy.KEEP_LAST,
            depth=1,
        )

        # ── Subscriptions ─────────────────────────────────────────────────────
        self.create_subscription(Image,     "drone/image_raw", self._cb_image,  qos)
        self.create_subscription(String,    "/drone/pose",     self._cb_pose,   qos)
        self.create_subscription(Imu,       "drone/imu",       self._cb_imu,    qos)
        self.create_subscription(Twist,     "cmd_vel",         self._cb_cmdvel, qos)
        self.create_subscription(TFMessage, "/tf",             self._cb_tf,     qos)

        # ── Corrected-image publisher ─────────────────────────────────────────
        self.img_pub = self.create_publisher(Image, "/camera/image_raw", 10)

        # ── 1 Hz health ticker ────────────────────────────────────────────────
        self.create_timer(1.0, self._health_tick)

        self.get_logger().info("DroneMonitorNode started — corrected image → /camera/image_raw")

    # ─── Internal helpers ─────────────────────────────────────────────────────

    def _stamp(self, topic: str):
        with self._lock:
            self.last_recv[topic] = time.time()
            self.msg_count[topic] += 1

    # ─── Image callback: enqueue only (fast, no heavy work) ──────────────────
    def _cb_image(self, msg: Image):
        self._stamp("drone/image_raw")
        try:
            self._img_q.put_nowait(msg)
        except queue.Full:
            pass  # drop frame; worker is behind

    # ─── Image worker thread ──────────────────────────────────────────────────
    def _image_worker(self):
        while True:
            try:
                msg = self._img_q.get(timeout=1.0)
            except queue.Empty:
                continue
            try:
                self._flip_and_publish(msg)
            except Exception as exc:
                self.get_logger().warn(f"[image_worker] {exc}")

    def _flip_and_publish(self, msg: Image):
        """
        Pure-numpy 180° rotation (flip vertically + horizontally).
        No cv_bridge, no OpenCV — avoids all cross-thread segfault risks.
        """
        h, w = msg.height, msg.width
        step = msg.step          # bytes per row (may include padding)
        bpp  = step // w         # bytes per pixel

        raw = np.frombuffer(bytes(msg.data), dtype=np.uint8).reshape(h, step)

        # Flip rows only (vertical flip) — fixes upside-down, keeps left/right correct
        pixel_cols = raw[:, : w * bpp].reshape(h, w, bpp)
        flipped_pixels = pixel_cols[::-1, :, :].reshape(h, w * bpp)

        if step > w * bpp:
            # Preserve any row-padding bytes (rare but correct)
            padding = raw[::-1, w * bpp:]
            flipped = np.concatenate([flipped_pixels, padding], axis=1)
        else:
            flipped = flipped_pixels

        out = Image()
        out.header       = msg.header
        out.height       = msg.height
        out.width        = msg.width
        out.encoding     = msg.encoding
        out.is_bigendian = msg.is_bigendian
        out.step         = msg.step
        out.data         = flipped.tobytes()
        self.img_pub.publish(out)

    # ─── Pose callback ────────────────────────────────────────────────────────
    def _cb_pose(self, msg: String):
        self._stamp("/drone/pose")
        try:
            parsed = {}
            for part in msg.data.split(","):
                k, v = part.strip().split(":")
                parsed[k.strip()] = float(v)
            with self._lock:
                self.pose_data.update(parsed)
        except Exception as exc:
            self.get_logger().warn(f"Pose parse error: {exc}  raw='{msg.data}'")
            return

        pd = self.pose_data
        self.get_logger().info(
            f"[POSE] Pos: ({pd['px']:+8.3f}, {pd['py']:+8.3f}, {pd['pz']:+8.3f})  "
            f"Euler: ({pd['rx']:+8.2f}°, {pd['ry']:+8.2f}°, {pd['rz']:+8.2f}°)"
        )

    # ─── IMU callback ─────────────────────────────────────────────────────────
    def _cb_imu(self, msg: Imu):
        self._stamp("drone/imu")
        with self._lock:
            self.imu_data.update(
                ax=msg.linear_acceleration.x,
                ay=msg.linear_acceleration.y,
                az=msg.linear_acceleration.z,
            )

    def _cb_cmdvel(self, msg: Twist):
        self._stamp("cmd_vel")

    def _cb_tf(self, msg: TFMessage):
        self._stamp("/tf")

    # ─── Health ticker ────────────────────────────────────────────────────────
    def _health_tick(self):
        now = time.time()
        with self._lock:
            counts = dict(self.msg_count)
            recvs  = dict(self.last_recv)

        self.get_logger().info("─" * 56)
        for topic in self.TOPICS:
            age    = now - recvs[topic]
            cnt    = counts[topic]
            status = "✓ OK   " if (cnt > 0 and age < 3.0) else "✗ STALE"
            self.get_logger().info(
                f"  {topic:28s} {status}  msgs={cnt:5d}  age={age:5.1f}s"
            )


# ─────────────────────────────────────────────────────────────────────────────
# GUI
# ─────────────────────────────────────────────────────────────────────────────
_BG, _CARD = "#1e1e2e", "#2a2a3e"
_FG, _ACC  = "#cdd6f4", "#89b4fa"
_GRN, _RED, _YEL = "#a6e3a1", "#f38ba8", "#f9e2af"
_MONO = ("Consolas", 10)


class DroneGUI:
    STALE_SECS = 3.0

    def __init__(self, node: DroneMonitorNode):
        self.node = node
        self.root = tk.Tk()
        self.root.title("Drone Monitor")
        self.root.configure(bg=_BG)
        self.root.geometry("680x570")
        self.root.resizable(True, True)
        self._build()
        self._schedule()

    # ── Build ─────────────────────────────────────────────────────────────────

    def _build(self):
        tk.Label(self.root, text="  Drone Monitor Dashboard",
                 bg=_BG, fg=_ACC, font=("Consolas", 14, "bold")).pack(pady=(12, 6))

        self._build_health()
        self._build_pose()
        self._build_imu()

        self.status_var = tk.StringVar(value="Initialising…")
        tk.Label(self.root, textvariable=self.status_var,
                 bg=_BG, fg="#585b70", font=("Consolas", 9)).pack(side="bottom", pady=5)

    def _card(self, title: str) -> tk.Frame:
        outer = tk.Frame(self.root, bg=_BG)
        outer.pack(fill="x", padx=16, pady=(4, 0))
        tk.Label(outer, text=title, bg=_BG, fg=_ACC,
                 font=("Consolas", 11, "bold")).pack(anchor="w")
        inner = tk.Frame(outer, bg=_CARD)
        inner.pack(fill="x")
        return inner

    def _build_health(self):
        card = self._card("Topic Health")
        self.t_status: dict[str, tk.Label] = {}
        self.t_count:  dict[str, tk.Label] = {}
        for topic, (msg_type, _) in DroneMonitorNode.TOPICS.items():
            row = tk.Frame(card, bg=_CARD)
            row.pack(fill="x", padx=10, pady=2)
            tk.Label(row, text=f"  {topic}", bg=_CARD, fg=_FG,
                     font=_MONO, width=28, anchor="w").pack(side="left")
            tk.Label(row, text=f"[{msg_type}]", bg=_CARD, fg=_YEL,
                     font=("Consolas", 9), width=14, anchor="w").pack(side="left")
            sl = tk.Label(row, text="● WAITING", bg=_CARD, fg=_YEL,
                          font=("Consolas", 10, "bold"), width=14, anchor="w")
            sl.pack(side="left")
            self.t_status[topic] = sl
            cl = tk.Label(row, text="0 msgs", bg=_CARD, fg=_FG,
                          font=("Consolas", 9), width=10, anchor="e")
            cl.pack(side="right", padx=6)
            self.t_count[topic] = cl

    def _build_pose(self):
        card  = self._card("Drone Pose")
        inner = tk.Frame(card, bg=_CARD)
        inner.pack(fill="x", padx=10, pady=6)
        self.pose_v: dict[str, tk.Label] = {}
        cols = [
            ("px", "Pos X",  _GRN), ("py", "Pos Y",  _GRN), ("pz", "Pos Z",  _GRN),
            ("rx", "Roll °", _YEL), ("ry", "Pitch °", _YEL), ("rz", "Yaw °",  _YEL),
        ]
        for c, (key, lbl, color) in enumerate(cols):
            f = tk.Frame(inner, bg=_CARD)
            f.grid(row=0, column=c, padx=8, pady=4, sticky="n")
            tk.Label(f, text=lbl, bg=_CARD, fg=_ACC,
                     font=("Consolas", 9, "bold")).pack()
            v = tk.Label(f, text="+0.0000", bg=_CARD, fg=color,
                         font=("Consolas", 12))
            v.pack()
            self.pose_v[key] = v

    def _build_imu(self):
        card  = self._card("IMU  Linear Acceleration  (m/s²)")
        inner = tk.Frame(card, bg=_CARD)
        inner.pack(fill="x", padx=10, pady=6)
        self.imu_v: dict[str, tk.Label] = {}
        for c, (key, lbl) in enumerate([("ax","X"), ("ay","Y"), ("az","Z")]):
            f = tk.Frame(inner, bg=_CARD)
            f.grid(row=0, column=c, padx=28, pady=4, sticky="n")
            tk.Label(f, text=lbl, bg=_CARD, fg=_ACC,
                     font=("Consolas", 9, "bold")).pack()
            v = tk.Label(f, text="+0.0000", bg=_CARD, fg=_FG,
                         font=("Consolas", 12))
            v.pack()
            self.imu_v[key] = v

    # ── Refresh (GUI thread only, via after()) ────────────────────────────────

    def _schedule(self):
        self._refresh()
        self.root.after(400, self._schedule)

    def _refresh(self):
        now = time.time()
        with self.node._lock:
            counts = dict(self.node.msg_count)
            recvs  = dict(self.node.last_recv)
            pose   = dict(self.node.pose_data)
            imu    = dict(self.node.imu_data)

        all_ok = True
        for topic, sl in self.t_status.items():
            age = now - recvs[topic]
            cnt = counts[topic]
            if cnt == 0:
                sl.config(text="● WAITING", fg=_YEL); all_ok = False
            elif age < self.STALE_SECS:
                sl.config(text="● ACTIVE",  fg=_GRN)
            else:
                sl.config(text="● STALE",   fg=_RED); all_ok = False
            self.t_count[topic].config(text=f"{cnt} msgs")

        for k, v in self.pose_v.items():
            v.config(text=f"{pose[k]:+.4f}")
        for k, v in self.imu_v.items():
            v.config(text=f"{imu[k]:+.4f}")

        ts  = datetime.now().strftime("%H:%M:%S")
        msg = f"All topics active  ✓   {ts}" if all_ok else f"Some topics inactive  ⚠   {ts}"
        self.status_var.set(msg)

    def run(self):
        self.root.mainloop()


# ─────────────────────────────────────────────────────────────────────────────
# Entry point
# ─────────────────────────────────────────────────────────────────────────────
def main():
    rclpy.init()
    node = DroneMonitorNode()

    # ROS spins in a background thread; tkinter MUST own the main thread
    threading.Thread(target=rclpy.spin, args=(node,), daemon=True).start()

    gui = DroneGUI(node)
    try:
        gui.run()
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == "__main__":
    main()