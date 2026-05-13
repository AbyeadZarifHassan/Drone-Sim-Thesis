# 🚁 Drone Simulation for VLM-Based Navigation — Thesis Project

A drone simulation environment and Vision-Language Model (VLM) agent scripts developed as part of an undergraduate thesis on autonomous drone navigation using Vision-Language Models integrated with Mission Planner.

---

## 📖 Overview

This repository contains the **simulation environment** and **VLM agent scripts** for a thesis project exploring how Vision-Language Models (VLMs) such as GPT, Gemini, LLaVA, LLaMA-Vision, and Qwen can be used to drive autonomous drone navigation decisions in a simulated 3D environment.

The simulation provides a controlled testbed where various VLM-based agents can perceive the environment, reason about navigation tasks, and issue control commands to a virtual drone.

> 💡 **Companion repository:** The Mission Planner integration code is maintained separately at [VLM_Navigation_with_Mission_Planner_Integration](https://github.com/AbyeadZarifHassan/VLM_Navigation_with_Mission_Planner_Integration).

---

## 🗂️ Repository Structure

```
Drone-Sim-Thesis/
├── simulation/              # Unity-based drone simulation environment
│   ├── Assets/              # 3D models, scenes, scripts
│   ├── Packages/            # Unity package dependencies
│   ├── ProjectSettings/     # Unity project configuration
│   └── Recordings/          # Sample simulation recordings
│
├── scripts/                 # Python agents for VLM-based navigation
│   ├── agent.py             # Base agent class
│   ├── agent_gemini.py      # Google Gemini-based agent
│   ├── agent_gpt5.py        # OpenAI GPT-based agent
│   ├── agent_llava.py       # LLaVA vision-language agent
│   ├── agent_llama_vision.py# LLaMA Vision agent
│   ├── agent_qwen.py        # Qwen vision-language agent
│   ├── agentv2.py           # Second-generation unified agent
│   ├── monitor.py           # Monitoring and logging utilities
│   └── string_cmd.py        # Command parsing utilities
│
├── .gitignore
└── README.md
```

---

## ⚙️ Requirements

### Simulation (Unity)
- **Unity Editor** 2021.3 LTS or later *(adjust to the version you used)*
- A system capable of running Unity scenes (Windows / macOS / Linux)

### Scripts (Python)
- **Python 3.9+**
- API keys for the VLM providers you intend to use:
  - OpenAI (for GPT-based agents)
  - Google AI Studio (for Gemini)
  - Local model setup for LLaVA / LLaMA Vision / Qwen (e.g., via Ollama or Hugging Face)

Install Python dependencies:
```bash
cd scripts
pip install -r requirements.txt
```
*(Note: add a `requirements.txt` listing your actual dependencies — e.g., `openai`, `google-generativeai`, `pillow`, `requests`, etc.)*

---

## 🚀 Getting Started

### 1. Clone the repository
```bash
git clone https://github.com/AbyeadZarifHassan/Drone-Sim-Thesis.git
cd Drone-Sim-Thesis
```

### 2. Open the Unity simulation
1. Open **Unity Hub**
2. Click **Add** → select the `simulation/` folder
3. Open the project and load the main scene
4. Press **Play** to start the simulation

### 3. Configure API keys
Create a `.env` file inside `scripts/` (this file is gitignored):
```
OPENAI_API_KEY=your_key_here
GEMINI_API_KEY=your_key_here
```

### 4. Run a VLM agent
```bash
cd scripts
python agent_gemini.py
# or
python agent_gpt5.py
```

---

## 🧠 How It Works

1. The **Unity simulation** renders a 3D environment containing a drone and objects/landmarks of interest.
2. A **VLM agent script** captures frames from the simulation (or receives them via a socket/file bridge).
3. The agent sends each frame, along with a navigation prompt, to a vision-language model.
4. The VLM returns a natural-language command (e.g., *"move forward"*, *"rotate left"*).
5. The command is parsed and sent back to the simulation to control the drone.
6. The cycle repeats until the goal is reached or a stop condition is met.

---

## 📊 Supported VLM Agents

| Agent | Backend | Type |
|---|---|---|
| `agent_gpt5.py` | OpenAI GPT (vision) | Cloud API |
| `agent_gemini.py` | Google Gemini | Cloud API |
| `agent_llava.py` | LLaVA | Local / Ollama |
| `agent_llama_vision.py` | LLaMA Vision | Local / Ollama |
| `agent_qwen.py` | Qwen-VL | Local / HuggingFace |

---

## 👥 Authors & Contributors

This thesis project is a collaborative effort:

- **Abyead Zarif Hassan** ([@AbyeadZarifHassan](https://github.com/AbyeadZarifHassan))
- **Tarannum Samiha** ([@Tarannum-Samiha](https://github.com/Tarannum-Samiha))
- Original simulation and agent script implementation by [@booleanwolf](https://github.com/booleanwolf)

---

## 🎓 Thesis Information

- **Institution:** *Your University Name*
- **Department:** *Your Department*
- **Supervisor:** *Supervisor's Name*
- **Year:** 2025–2026

*(Please fill in the placeholders above.)*

---

## 📜 License

This project is intended for academic and research purposes. If you wish to reuse or extend this work, please cite the thesis and the contributors above.

---

## 🙏 Acknowledgments

- Thanks to our thesis supervisor for guidance.
- Thanks to [@booleanwolf](https://github.com/booleanwolf) for contributions to the original simulation and agent code.
- Built using Unity and open-source VLM frameworks.

---

## 📧 Contact

For questions or collaboration:
- **Abyead Zarif Hassan** — abyeadzarif3139@gmail.com
