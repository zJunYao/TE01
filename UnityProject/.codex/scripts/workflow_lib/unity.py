"""Known command policies and Unity Pipeline response validation."""

from __future__ import annotations

import json
from pathlib import Path
import re
import time

from .core import WorkflowError, blocked, consume_approval, digest, make_plan


READ_ONLY = frozenset({
    "editor_status", "list_open_scenes", "get_scene_hierarchy", "get_build_settings",
    "list_build_targets", "list_build_profiles", "list_tests", "test_status", "recompile_status",
    "get_player_settings", "get_audio_settings", "get_graphics_settings", "get_input_settings",
    "get_physics_settings", "get_quality_settings", "get_tags_layers", "get_time_settings",
    "get_console_logs", "get_performance_stats", "get_authoring_root", "get_selection",
    "find_assets", "find_gameobjects", "get_component_properties", "get_serialized_fields",
    "get_import_settings", "get_material_properties", "list_shaders", "get_shader_properties",
    "get_animation_clip", "get_animator_controller", "get_timeline", "get_lighting_settings",
    "get_navmesh_settings", "build_status", "switch_build_target_status", "package_list",
    "package_status", "lighting_bake_status", "navmesh_bake_status", "occlusion_bake_status",
    "tengine_validate_assets",
    "tengine_validate_game_test_scene",
    "tengine_validate_bun_idle",
    "tengine_validate_bun_motion",
    "tengine_validate_bun_death",
    "tengine_validate_mold_idle",
    "tengine_validate_mold_dissolve",
    "tengine_validate_mold_effect",
    "tengine_validate_mold_native_preview",
})

# Explicit catalog, not a prefix heuristic. Only implemented return/poll contracts are admitted.
MUTATIONS = {
    "tengine_configure_mold_native_burst": {"dry_run": True, "confirm": True, "poll": None},
    "tengine_replace_mold_smoke": {"dry_run": True, "confirm": True, "poll": None},
    "tengine_extract_mold_effect": {"dry_run": True, "confirm": True, "poll": None},
    "tengine_build_mold_burst": {"dry_run": True, "confirm": True, "poll": None},
    "tengine_build_mold_dissolve": {"dry_run": True, "confirm": True, "poll": None},
    "tengine_fix_mold_idle_state": {"dry_run": True, "confirm": True, "poll": None},
    "tengine_build_mold_idle": {"dry_run": True, "confirm": True, "poll": None},
    "tengine_fix_bun_death_state": {"dry_run": True, "confirm": True, "poll": None},
    "tengine_build_bun_death": {"dry_run": True, "confirm": True, "poll": None},
    "tengine_fix_bun_motion_states": {"dry_run": True, "confirm": True, "poll": None},
    "tengine_build_bun_motion": {"dry_run": True, "confirm": True, "poll": None},
    "tengine_build_bun_idle": {"dry_run": True, "confirm": True, "poll": None},
    "tengine_fix_bun_idle_duration": {"dry_run": True, "confirm": True, "poll": None},
    "create_folder": {"dry_run": False, "confirm": False, "poll": None},
    "move_asset": {"dry_run": True, "confirm": False, "poll": None},
    "import_asset": {"dry_run": True, "confirm": True, "poll": None},
    "set_import_settings": {"dry_run": True, "confirm": False, "poll": None},
    "tengine_bake_ugui": {"dry_run": True, "confirm": True, "poll": None},
    "tengine_create_game_test_scene": {"dry_run": True, "confirm": True, "poll": None},
    "tengine_capture_game_test_scene": {"dry_run": True, "confirm": True, "poll": None},
    "delete_asset": {"dry_run": True, "confirm": True, "poll": None},
    "set_player_settings": {"dry_run": True, "confirm": True, "poll": None},
    "set_build_settings": {"dry_run": True, "confirm": True, "poll": None},
    "build": {"dry_run": True, "confirm": True, "poll": "build_status"},
}

RESERVED = {"project-path", "project_path", "runtime", "runtime-path", "runtime_path",
            "format", "json", "confirm", "dry_run", "dry-run", "non-interactive", "timeout"}

STATUS_QUERIES = frozenset({
    "editor_status", "test_status", "recompile_status", "build_status", "switch_build_target_status",
    "package_status", "lighting_bake_status", "navmesh_bake_status", "occlusion_bake_status",
    "get_console_logs",
})


def validate_parameters(parameters):
    if not isinstance(parameters, dict):
        raise WorkflowError("Parameters must be a JSON object.")
    for key in parameters:
        if not key or key.startswith("-") or key in RESERVED or not key.replace("_", "").isalnum():
            raise WorkflowError(f"Reserved or invalid parameter: {key}")
    return parameters


def decode_response(text):
    try:
        current = json.loads(text.lstrip("\ufeff"))
    except (ValueError, AttributeError) as exc:
        raise WorkflowError("CLI did not return a JSON response; inspect command evidence.") from exc
    # CLI envelope -> Pipeline envelope -> JSON-string status. Never search arbitrary asset data.
    for _ in range(6):
        if isinstance(current, str):
            try:
                parsed = json.loads(current)
            except ValueError:
                return current
            current = parsed
            continue
        if not isinstance(current, dict):
            return current
        if current.get("success") is False or current.get("Success") is False:
            raise WorkflowError("Command/business failure: " + json.dumps(current, ensure_ascii=False))
        if current.get("status") in ("error", "failed", "cancelled", "busy"):
            raise WorkflowError("Operation did not succeed: " + json.dumps(current, ensure_ascii=False))
        if current.get("errors"):
            raise WorkflowError("Command returned errors: " + json.dumps(current["errors"], ensure_ascii=False))
        if "data" in current and ("success" in current or "command" in current):
            current = current["data"]
        elif ("result" in current and ("command" in current or "executionTimeMs" in current)
              and not any(key in current for key in ("summary", "Summary", "statusPath", "StatusPath", "tests", "Tests"))):
            current = current["result"]
        else:
            return current
    raise WorkflowError("Unexpectedly deep response envelope.")


def parse_instances(text, project, require_ready=True):
    data = decode_response(text)
    if not isinstance(data, dict) or not isinstance(data.get("instances"), list):
        blocked("Unknown CLI status schema; do not guess the selected project.")
    instances = data["instances"]
    if len(instances) != 1 or data.get("count", len(instances)) != 1:
        blocked(f"Expected exactly one project instance; received {len(instances)}.")
    instance = instances[0]
    actual = instance.get("projectPath")
    if not actual and isinstance(instance.get("project"), dict):
        actual = instance["project"].get("path")
    if not actual and isinstance(instance.get("project"), str):
        actual = instance["project"]
    if not actual or Path(actual).resolve() != Path(project).resolve():
        blocked("CLI instance project does not match the requested absolute project path.")
    state = instance.get("state", instance.get("status"))
    if isinstance(state, dict):
        state = state.get("status")
    if not state or (require_ready and state != "ready"):
        blocked(f"Editor is not ready ({state!r}); wait for compilation/import/testing to finish.")
    return instance


def command_names(value):
    if isinstance(value, dict):
        for key in ("commands", "tools"):
            if isinstance(value.get(key), list):
                return {item.get("name") or item.get("command") for item in value[key] if isinstance(item, dict)}
        if "result" in value:
            return command_names(value["result"])
    if isinstance(value, list):
        return {item.get("name") or item.get("command") for item in value if isinstance(item, dict)}
    blocked("Unrecognized command discovery schema; inspect the registered capabilities.")


def test_listing(value, mode, query, filter_type):
    if not isinstance(value, dict):
        raise WorkflowError("Invalid test listing.")
    tests = value.get("tests", value.get("Tests"))
    if not isinstance(tests, list):
        raise WorkflowError("Test listing lacks individual test identities.")
    expected_mode = {"editor": "EditMode", "playmode": "PlayMode"}[mode]
    names = []
    for test in tests:
        name = test.get("fullName", test.get("FullName", ""))
        test_mode = test.get("mode", test.get("Mode", ""))
        explicit = test.get("explicit", test.get("Explicit", False))
        categories = test.get("categories", test.get("Categories", [])) or []
        value = {"testName": name, "assembly": test.get("assembly", test.get("Assembly", "")),
                 "category": ""}[filter_type]
        matches = (any(query.lower() == category.lower() for category in categories) if filter_type == "category"
                   else query.lower() in value.lower())
        if not explicit and test_mode.lower() == expected_mode.lower() and matches:
            names.append(name)
    if not names:
        blocked(f"No runnable {mode} tests match {filter_type}={query!r}.")
    return set(names)


def validate_test_result(value, expected_names):
    if not isinstance(value, dict) or value.get("status") != "completed":
        raise WorkflowError("Tests have not reached a completed state.")
    summary = value.get("summary", {})
    fields = ("total", "passed", "failed", "skipped", "inconclusive")
    if any(not isinstance(summary.get(key), int) or summary[key] < 0 for key in fields):
        raise WorkflowError("Invalid test summary.")
    if summary["total"] == 0 or summary["passed"] == 0:
        raise WorkflowError("Zero executed/passed tests, or all tests skipped.")
    if summary["failed"] or summary["inconclusive"]:
        raise WorkflowError("Tests failed or were inconclusive.")
    if summary["total"] != sum(summary[key] for key in fields[1:]):
        raise WorkflowError("Test summary counts are inconsistent.")
    results = value.get("results", [])
    if not isinstance(results, list) or len(results) != summary["total"]:
        raise WorkflowError("Test result list/count mismatch.")
    actual_counts = {"passed": 0, "failed": 0, "skipped": 0, "inconclusive": 0}
    for item in results:
        status = str(item.get("status", item.get("Status", ""))).lower()
        if status not in actual_counts:
            raise WorkflowError("Unknown individual test outcome.")
        actual_counts[status] += 1
    if any(summary[key] != count for key, count in actual_counts.items()):
        raise WorkflowError("Individual test outcomes disagree with the summary.")
    names = {item.get("fullName", item.get("FullName", item.get("name", item.get("Name"))))
             for item in results if isinstance(item, dict)}
    if names != expected_names or summary["total"] != len(expected_names):
        raise WorkflowError("Test identities/count differ from the selected discovery set; stale or changed result.")
    return summary


class Unity:
    def __init__(self, run):
        self.run = run
        self.context = run.context
        self.capabilities = None
        self.cli_mode = None

    def detect_cli(self):
        """按实际帮助识别协议，避免把编辑器同名程序作为 CLI 使用。"""
        if self.cli_mode is not None:
            return self.cli_mode
        if not self.context.cli.is_file():
            blocked(f"未找到 Unity CLI：{self.context.cli}；可通过 UNITY_CLI_PATH 指定已有 CLI。")
        result = self.run.execute([self.context.cli, "--help"], timeout=15)
        if result.returncode:
            blocked("无法读取 Unity CLI 帮助；请检查指定的程序是否为 Unity CLI。")
        names = set(re.findall(r"^\s{2}([\w-]+)(?:[|\s])", result.stdout, re.M))
        if {"command", "status"} <= names:
            self.cli_mode = "legacy"
        elif {"request", "pipeline"} <= names:
            self.cli_mode = "request"
        else:
            blocked("不支持的 Unity CLI 命令协议；不会自动安装、升级或转用 Unity 编辑器。")
        return self.cli_mode

    def raw(self, arguments, timeout=45, mutating=False):
        mode = self.detect_cli()
        arguments = list(arguments)
        if mode == "request" and arguments and arguments[0] == "command":
            arguments[0] = "request"
        flags = ["--no-log-proxy"] if mode == "legacy" else ["--quiet"]
        result = self.run.execute(
            [self.context.cli, *arguments, "--format", "json", "--non-interactive", *flags],
            timeout=timeout, mutating=mutating,
        )
        if result.returncode:
            # Connection errors are blockers, never a successful validation.
            if any(code in result.stdout + result.stderr for code in ("NO_INSTANCES", "UNREACHABLE", "CONNECTION", "ECONNREFUSED", "TIMEOUT",
                                                       "502 Bad Gateway", "503 Service Unavailable", "No Pipeline instance found")):
                blocked("Unity CLI connection unavailable; see command evidence.")
            raise WorkflowError(f"Unity CLI exited with {result.returncode}; see command evidence.")
        return result.stdout

    def ready(self, require_ready=True):
        if self.detect_cli() == "request":
            data = decode_response(self.raw(["pipeline", "list"]))
            if not isinstance(data, dict) or not isinstance(data.get("instances"), list):
                blocked("无法识别 CLI 实例列表，停止以避免连接错误工程。")
            matches = [item for item in data["instances"] if isinstance(item, dict)
                       and isinstance(item.get("projectPath"), str)
                       and Path(item["projectPath"]).resolve() == self.context.project.resolve()]
            if len(matches) != 1:
                blocked(f"当前工程必须恰有一个 Unity 实例，实际为 {len(matches)}。")
            instance = matches[0]
            if instance.get("isRunning") is not True or not instance.get("pipelineServer"):
                blocked("CLI 未发现当前工程的 Pipeline 服务；这不等于服务未启动。若编辑器已启动服务，请检查 CLI 与 Pipeline 的发现及鉴权协议兼容性。")
            status = decode_response(self.raw(["command", "--project-path", str(self.context.project), "editor_status"]))
            if not isinstance(status, dict):
                blocked("无法识别当前工程的 Editor 状态。")
            return parse_instances(json.dumps({"instances": [status]}), self.context.project, require_ready)
        return parse_instances(self.raw(["status", "--project-path", str(self.context.project)]),
                               self.context.project, require_ready)

    def discover(self, require_ready=True):
        self.ready(require_ready)
        value = decode_response(self.raw(["command", "--project-path", str(self.context.project)]))
        self.capabilities = command_names(value)
        return sorted(name for name in self.capabilities if name)

    def call(self, command, parameters=None, timeout=45, mutating=False):
        if self.capabilities is None:
            self.discover()
        if command not in self.capabilities:
            blocked(f"Command is not registered in this project: {command}")
        arguments = ["command", "--project-path", str(self.context.project),
                     "--timeout", str(timeout), command]
        for key, value in (parameters or {}).items():
            arguments.extend(["--" + key, json.dumps(value, ensure_ascii=False) if not isinstance(value, str) else value])
        return decode_response(self.raw(arguments, timeout=timeout + 5, mutating=mutating))

    def query(self, command, parameters):
        validate_parameters(parameters)
        if command not in READ_ONLY:
            blocked(f"Not an audited read-only command: {command}")
        status_query = command in STATUS_QUERIES
        self.ready(require_ready=not status_query)
        if self.capabilities is None:
            self.discover(require_ready=not status_query)
        return self.call(command, parameters)

    def poll(self, command, terminal, timeout=300, interval=1):
        deadline = time.monotonic() + timeout
        errors = 0
        while time.monotonic() < deadline:
            try:
                # Domain reload may reconnect to a restarted/other instance; rebind every observation.
                self.ready(require_ready=False)
                value = self.call(command, timeout=min(15, max(1, int(deadline - time.monotonic()))))
                errors = 0
            except WorkflowError as exc:
                if exc.status != "blocked":
                    raise
                errors += 1
                if errors > 5:
                    raise
                time.sleep(interval)
                continue
            if isinstance(value, dict) and value.get("status") in terminal:
                return value
            time.sleep(interval)
        blocked(f"Timed out polling {command}. Operation may still be active; do not replay it.")

    def recompile(self, timeout):
        self.ready()
        started = self.call("recompile", mutating=True)
        if not isinstance(started, dict) or started.get("status") not in ("compiling", "up_to_date"):
            blocked("Compilation start not acknowledged; retained results cannot establish success.")
        try:
            value = self.poll("recompile_status", {"completed", "up_to_date"}, timeout)
        except WorkflowError:
            # Import/asmdef errors can prevent compilation callbacks entirely.
            # Collect the read-only console without hiding the original incomplete operation.
            try:
                self.call("get_console_logs")
            except WorkflowError:
                pass
            raise
        if value.get("success") is False or value.get("failed") or value.get("hasErrors") or value.get("errorCount", 0):
            raise WorkflowError("Editor compilation reported errors.")
        self.ready()
        self.discover()
        logs = self.call("get_console_logs")
        return {"compilation": value, "console": logs}

    def tests(self, mode, query, filter_type, timeout):
        self.ready()
        expected = test_listing(self.call("list_tests", {"mode": mode}), mode, query, filter_type)
        before = self.call("test_status")
        if isinstance(before, dict) and before.get("status") == "running":
            blocked("Another test run is active.")
        # The package synchronously clears prior status before acknowledging the start.
        # An unacknowledged start is ambiguous; never fall back to a retained completed result.
        started = self.call("run_tests", {"mode": mode, "filter": query, "filter_type": filter_type,
                                        "async_tests": True}, mutating=True)
        if not isinstance(started, dict) or started.get("result", started.get("Result")) != "running":
            blocked("Async test start was not acknowledged; do not accept the last retained result.")
        value = self.poll("test_status", {"completed"}, timeout)
        summary = validate_test_result(value, expected)
        return {"mode": mode, "filter": query, "summary": summary, "results": value["results"]}

    def preview(self, command, parameters, inputs=()):
        validate_parameters(parameters)
        if command not in MUTATIONS:
            blocked(f"Mutation policy/return contract not implemented: {command}")
        self.ready()
        policy = MUTATIONS[command]
        preview = self.call(command, {**parameters, "dry_run": True}) if policy["dry_run"] else {"staticOnly": True}
        if command == "set_import_settings" and preview.get("unknown"):
            blocked("存在无法应用的导入设置，停止写入：" + str(preview["unknown"]))
        if command == "import_asset":
            inputs = (*inputs, parameters["source"])
        return make_plan(self.run, "unity", command, parameters, high_risk=True, inputs=inputs, preview=preview)

    def apply(self, plan, approval_file):
        if plan.get("kind") != "unity" or plan.get("command") not in MUTATIONS:
            blocked("Unsupported Unity mutation plan.")
        validate_parameters(plan["parameters"])
        self.ready()
        self.discover()
        command = plan["command"]
        if command not in self.capabilities:
            blocked(f"Missing capability: {command}")
        current_preview = (self.call(command, {**plan["parameters"], "dry_run": True})
                           if MUTATIONS[command]["dry_run"] else {"staticOnly": True})
        if digest(current_preview) != digest(plan.get("preview")):
            blocked("Editor preview changed since authorization; inspect the new state and authorize a fresh preview.")
        consume_approval(self.run, plan, approval_file)
        arguments = dict(plan["parameters"])
        if MUTATIONS[command]["dry_run"]:
            arguments["dry_run"] = False
        if MUTATIONS[command]["confirm"]:
            arguments["confirm"] = True
        result = self.call(command, arguments, mutating=True)
        poll = MUTATIONS[command]["poll"]
        if poll:
            if not isinstance(result, dict) or result.get("status") != "queued":
                raise WorkflowError("Build was not queued.")
            final = self.poll(poll, {"completed"}, 1800)
            if final.get("buildId") != result.get("buildId") or not final.get("buildId"):
                raise WorkflowError("Build result does not match the submitted operation.")
            if final.get("result") != "Succeeded":
                raise WorkflowError("Player build did not succeed.")
            return final
        return result
