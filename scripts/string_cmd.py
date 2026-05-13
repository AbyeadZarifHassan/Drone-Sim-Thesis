#!/usr/bin/env python3
"""
ROS2 Humble Node: string_to_cmdvel
Subscribes to /motion_command (std_msgs/String) and publishes geometry_msgs/Twist to /cmd_vel.

Supported commands:
  Translations:
    forward         +X
    backward        -X
    left            +Y
    right           -Y
    up              +Z
    down            -Z

  Diagonals (XY plane):
    forward_left    +X +Y
    forward_right   +X -Y
    backward_left   -X +Y
    backward_right  -X -Y

  Yaw rotations:
    yaw_left        +Z angular (CCW)
    yaw_right       -Z angular (CW)

  Stop:
    stop            all zeros

Usage:
  ros2 run <your_package> string_to_cmdvel
  ros2 topic pub /motion_command std_msgs/String "data: 'forward'"
"""

import rclpy
from rclpy.node import Node
from std_msgs.msg import String
from geometry_msgs.msg import Twist
import math


class StringToCmdVel(Node):
    def __init__(self):
        super().__init__('string_to_cmdvel')

        # Declare and read parameters
        self.declare_parameter('linear_speed', 0.5)             # m/s
        self.declare_parameter('angular_speed', math.pi / 4)    # rad/s (~45 deg/s)
        self.declare_parameter('diagonal_scale', 1.0 / math.sqrt(2))  # normalize diagonal
        self.declare_parameter('command_duration', 0.5)         # seconds to run before stopping

        self.linear_speed = self.get_parameter('linear_speed').value
        self.angular_speed = self.get_parameter('angular_speed').value
        self.diagonal_scale = self.get_parameter('diagonal_scale').value
        self.command_duration = self.get_parameter('command_duration').value

        # Publisher
        self.pub = self.create_publisher(Twist, '/cmd_vel', 10)

        # Subscriber
        self.sub = self.create_subscription(
            String,
            '/motion_command',
            self.command_callback,
            10
        )

        # Timer handle for auto-stop (None when idle)
        self._stop_timer = None

        self.get_logger().info(
            f'string_to_cmdvel ready. '
            f'linear_speed={self.linear_speed} m/s, '
            f'angular_speed={self.angular_speed:.3f} rad/s, '
            f'command_duration={self.command_duration} s'
        )

    def build_twist(self, lx=0.0, ly=0.0, lz=0.0, ax=0.0, ay=0.0, az=0.0) -> Twist:
        msg = Twist()
        msg.linear.x = lx
        msg.linear.y = ly
        msg.linear.z = lz
        msg.angular.x = ax
        msg.angular.y = ay
        msg.angular.z = az
        return msg

    def _cancel_stop_timer(self):
        """Cancel any pending auto-stop timer."""
        if self._stop_timer is not None:
            self._stop_timer.cancel()
            self._stop_timer = None

    def _auto_stop(self):
        """Called after command_duration seconds — publish zero twist."""
        self._cancel_stop_timer()
        self.pub.publish(self.build_twist())
        self.get_logger().info('Auto-stop: published zero cmd_vel.')

    def command_callback(self, msg: String):
        cmd = msg.data.strip().lower()
        v = self.linear_speed
        w = self.angular_speed
        d = v * self.diagonal_scale  # diagonal component speed

        command_map = {
            # ── Cardinal translations ──────────────────────────────────────
            'forward':        self.build_twist(lx=+v),
            'backward':       self.build_twist(lx=-v),
            'left':           self.build_twist(ly=+v),
            'right':          self.build_twist(ly=-v),
            'up':             self.build_twist(lz=+v),
            'down':           self.build_twist(lz=-v),

            # ── Diagonal translations (XY plane) ───────────────────────────
            'forward_left':   self.build_twist(lx=+d, ly=+d),
            'forward_right':  self.build_twist(lx=+d, ly=-d),
            'backward_left':  self.build_twist(lx=-d, ly=+d),
            'backward_right': self.build_twist(lx=-d, ly=-d),

            # ── Yaw rotations ──────────────────────────────────────────────
            'yaw_left':       self.build_twist(az=+w),   # CCW (positive Z per REP-103)
            'yaw_right':      self.build_twist(az=-w),   # CW

            # ── Stop (immediate, no timer) ─────────────────────────────────
            'stop':           self.build_twist(),
        }

        if cmd == 'stop':
            # Immediate manual stop — cancel any running timer
            self._cancel_stop_timer()
            self.pub.publish(self.build_twist())
            self.get_logger().info('[stop] -> immediate stop.')
            return

        if cmd in command_map:
            twist = command_map[cmd]

            # If a previous command is still running, cancel its stop timer
            # before starting the new one (new command takes over immediately)
            self._cancel_stop_timer()

            # Publish the motion command
            self.pub.publish(twist)
            self.get_logger().info(
                f"[{cmd}] -> lin=({twist.linear.x:.2f}, {twist.linear.y:.2f}, {twist.linear.z:.2f}) "
                f"ang=({twist.angular.x:.2f}, {twist.angular.y:.2f}, {twist.angular.z:.2f}) "
                f"| stopping in {self.command_duration}s"
            )

            # Schedule auto-stop after command_duration seconds (one-shot timer)
            self._stop_timer = self.create_timer(
                self.command_duration,
                self._auto_stop
            )
        else:
            self.get_logger().warn(
                f"Unknown command: '{cmd}'. Valid commands: "
                f"{list(command_map.keys())}"
            )


def main(args=None):
    rclpy.init(args=args)
    node = StringToCmdVel()
    try:
        rclpy.spin(node)
    except KeyboardInterrupt:
        pass
    finally:
        node.destroy_node()
        rclpy.shutdown()


if __name__ == '__main__':
    main()