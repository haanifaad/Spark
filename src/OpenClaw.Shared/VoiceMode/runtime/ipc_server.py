import sys
import json
import os
import threading
import traceback
from pathlib import Path

# Add current dir to path
sys.path.insert(0, str(Path(__file__).resolve().parent))

from security_guard import guard_check, set_active_division, is_voice_mode

# Import MARK / Voice Mode action modules
import actions.computer_control as computer_control_mod
import actions.computer_settings as computer_settings_mod
import actions.screen_processor as screen_processor_mod
import actions.file_processor as file_processor_mod
import actions.file_controller as file_controller_mod
import actions.browser_control as browser_control_mod
import actions.open_app as open_app_mod
import actions.dev_agent as dev_agent_mod
import actions.code_helper as code_helper_mod
import actions.web_search as web_search_mod
import actions.weather_report as weather_report_mod
import actions.reminder as reminder_mod
import actions.flight_finder as flight_finder_mod
import actions.youtube_video as youtube_video_mod
import agent.planner as planner_mod
import agent.executor as executor_mod
import agent.task_queue as task_queue_mod
import memory.memory_manager as memory_manager_mod

task_queue = task_queue_mod.TaskQueue()
task_queue.start()

class MockUI:
    def __init__(self):
        self.muted = False
    def write_log(self, text):
        pass
    def set_state(self, state):
        pass

mock_ui = MockUI()

def handle_request(req: dict) -> dict:
    cmd = req.get("command", "")
    division = req.get("division", "voice-mode")
    set_active_division(division)

    # Capability guard check
    allowed, rejection = guard_check(cmd)
    if not allowed:
        return {"success": False, "error": rejection, "command": cmd}

    params = req.get("params", {})

    try:
        if cmd == "ping":
            return {"success": True, "result": "pong", "active_division": division}

        elif cmd == "set_division":
            new_div = params.get("division", "voice-mode")
            set_active_division(new_div)
            return {"success": True, "active_division": new_div}

        # GROUP 1: Real-time voice intelligence queries
        elif cmd == "voice_interact" or cmd == "text_interact":
            prompt = params.get("text", "")
            # Return status or query
            return {"success": True, "result": f"Voice mode processed prompt: {prompt}"}

        # GROUP 2: Visual Awareness
        elif cmd == "screen_capture":
            # Attempt screen capture
            data = screen_processor_mod._capture_screenshot()
            return {"success": True, "bytes_length": len(data), "format": "jpeg"}

        elif cmd == "visual_qa":
            query = params.get("query", "Describe what you see.")
            mode = params.get("mode", "screen")
            res = screen_processor_mod.screen_process({"query": query, "mode": mode}, mock_ui)
            return {"success": True, "result": res}

        # GROUP 3: Advanced Computer Control
        elif cmd == "computer_settings":
            res = computer_settings_mod.computer_settings(params, mock_ui)
            return {"success": True, "result": res}

        elif cmd == "computer_control":
            res = computer_control_mod.computer_control(params, mock_ui)
            return {"success": True, "result": res}

        elif cmd == "open_app":
            app_name = params.get("app_name", "")
            res = open_app_mod.open_app({"app_name": app_name}, mock_ui)
            return {"success": True, "result": res}

        elif cmd == "browser_control":
            res = browser_control_mod.browser_control(params, mock_ui)
            return {"success": True, "result": res}

        elif cmd == "file_controller":
            res = file_controller_mod.file_controller(params, mock_ui)
            return {"success": True, "result": res}

        # GROUP 4: Autonomous Task Execution
        elif cmd == "submit_task":
            goal = params.get("goal", "")
            tid = task_queue.submit(goal)
            return {"success": True, "task_id": tid}

        elif cmd == "get_task_status":
            tid = params.get("task_id", "")
            status = task_queue.get_status(tid)
            return {"success": True, "status": status}

        # GROUP 5: Persistent Memory
        elif cmd == "load_memory":
            m = memory_manager_mod.load_memory()
            return {"success": True, "memory": m}

        elif cmd == "update_memory":
            m_update = params.get("data", {})
            m = memory_manager_mod.update_memory(m_update)
            return {"success": True, "memory": m}

        # GROUP 6: File Intelligence
        elif cmd == "process_file":
            res = file_processor_mod.file_processor(params, mock_ui)
            return {"success": True, "result": res}

        else:
            return {"success": False, "error": f"Unknown command '{cmd}'"}

    except Exception as e:
        return {"success": False, "error": str(e), "traceback": traceback.format_exc()}


def main():
    # If run in stdio server mode
    for line in sys.stdin:
        line = line.strip()
        if not line:
            continue
        try:
            req = json.loads(line)
            resp = handle_request(req)
            sys.stdout.write(json.dumps(resp) + "\n")
            sys.stdout.flush()
        except Exception as e:
            sys.stdout.write(json.dumps({"success": False, "error": str(e)}) + "\n")
            sys.stdout.flush()

if __name__ == "__main__":
    if len(sys.argv) > 1 and sys.argv[1] == "--server":
        main()
    elif len(sys.argv) > 1 and sys.argv[1] == "--ui":
        # Launch UI directly
        import main as voice_main
        voice_main.main()
    else:
        main()
