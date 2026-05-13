# 🚁 Autonomous Drone Simulation — Unity + ROS2

A ROS2-integrated autonomous drone simulation built in Unity. Supports real-time command input, system monitoring, and AI-driven agent control through a TCP endpoint bridge.

---

## 📋 Table of Contents

- [Prerequisites](#prerequisites)
- [Project Setup](#project-setup)
- [ROS Workspace Setup](#ros-workspace-setup)
- [Running the Simulation](#running-the-simulation)
- [Running the Scripts](#running-the-scripts)
- [Architecture Overview](#architecture-overview)

---

## ✅ Prerequisites

Before getting started, make sure you have the following installed:

- [Unity Hub](https://unity.com/download) with a compatible Unity Editor version
- [ROS2](https://docs.ros.org/en/humble/Installation.html) (Humble or newer recommended)
- Python 3.8+
- `git`, `unzip`, `colcon`

---

## 🗂️ Project Setup

### 1. Clone / Download the Unity Project

Download or clone the Unity project repository to your local machine.

### 2. Open in Unity

1. Open **Unity Hub**
2. Click **Add project from disk** and select the project root folder
3. Wait for Unity to import all assets

### 3. Load the Drone Scene

In the Unity **Project** panel, navigate to:

```
Assets/__Drone/
```

Open the scene file located there. Press ▶ **Play** to start the simulation once the ROS endpoint is running.

---

## 🔧 ROS Workspace Setup

### 1. Obtain ROS TCP Endpoint

You should have received the `ROS_TCP_ENDPOINT.zip` file directly (e.g. via Telegram). Place it in your home directory before continuing.

### 2. Unzip and Place into Workspace

```bash
unzip ROS_TCP_ENDPOINT.zip -d ~/ws/src/
```

> Your workspace source directory should now contain the `ROS-TCP-Endpoint-main` folder inside `~/ws/src/`.

### 3. Build the Package

```bash
cd ~/ws
colcon build --packages-select ros_tcp_endpoint
```

### 4. Source the Workspace

```bash
source ~/ws/install/setup.bash
```

> 💡 **Tip:** Add this line to your `~/.bashrc` so it's automatically sourced in every new terminal:
> ```bash
> echo "source ~/ws/install/setup.bash" >> ~/.bashrc
> ```

---

## ▶️ Running the Simulation

### Start the ROS TCP Endpoint

Open a terminal and run the default ROS TCP Endpoint server:

```bash
ros2 run ros_tcp_endpoint default_server_endpoint --ros-args -p ROS_IP:=127.0.0.1
```

This starts the bridge between Unity and ROS2 on `localhost`. Keep this terminal open.

### Play the Unity Scene

Back in Unity, press ▶ **Play** to start the drone simulation. The Unity client will connect to the ROS TCP endpoint automatically.

---

## 🐍 Running the Scripts

Open **three separate terminals** and run each of the following scripts in order:

### Terminal 1 — Command Input

```bash
python3 string_cmd.py
```

Handles string-based command input to control the drone.

---

### Terminal 2 — System Monitor

```bash
python3 monitor.py
```

Monitors and displays real-time system state, telemetry, and ROS topic activity.

---

### Terminal 3 — AI Agent

```bash
python3 agentv2.py
```

Runs the autonomous agent that drives drone decision-making and path planning.

---

## 🏗️ Architecture Overview

```
┌─────────────────────┐         ┌──────────────────────┐
│   Unity Simulation  │◄───────►│  ROS TCP Endpoint    │
│  (Assets/__Drone)   │  TCP    │  (port 10000)        │
└─────────────────────┘         └──────────┬───────────┘
                                            │ ROS2 Topics
                        ┌───────────────────┼───────────────────┐
                        ▼                   ▼                   ▼
               string_cmd.py          monitor.py           agentv2.py
               (Commands)             (Monitoring)          (AI Agent)
```

| Component | Role |
|---|---|
| **Unity Scene** | Renders the 3D drone simulation environment |
| **ROS TCP Endpoint** | Bridges Unity ↔ ROS2 communication over TCP |
| `string_cmd.py` | Sends string commands to control the drone |
| `monitor.py` | Monitors system state and ROS topics in real time |
| `agentv2.py` | Autonomous AI agent for drone navigation and decision-making |

---

## 🛠️ Troubleshooting

**Unity can't connect to ROS endpoint**
- Ensure `default_server_endpoint` is running before pressing Play in Unity
- Confirm the IP address matches (`127.0.0.1` for local setups)

**`colcon build` fails**
- Make sure you've sourced your base ROS2 installation: `source /opt/ros/humble/setup.bash`

**Python scripts can't find ROS topics**
- Source the workspace in each new terminal: `source ~/ws/install/setup.bash`

---

## 📄 License

See [LICENSE](LICENSE) for details.
