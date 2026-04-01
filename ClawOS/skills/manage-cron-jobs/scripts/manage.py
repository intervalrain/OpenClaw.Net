#!/usr/bin/env python3
"""
Cron Job Management Script
Called by the manage-cron-jobs skill to interact with the ClawOS API.
Reads auth token from environment or ~/.clawos/auth.json.

Usage:
    python3 manage.py list-jobs
    python3 manage.py list-tools
    python3 manage.py list-tool-instances
    python3 manage.py list-skills
    python3 manage.py create-job '<json>'
    python3 manage.py create-tool-instance '<json>'
    python3 manage.py execute-job <job-id>
    python3 manage.py delete-job <job-id>
    python3 manage.py delete-tool-instance <instance-id>
"""

import sys
import json
import os
import urllib.request
import urllib.error

BASE_URL = os.environ.get("CLAWOS_URL", "http://localhost:5002")
API_BASE = f"{BASE_URL}/api/v1"


def get_token():
    """Get auth token from env or auth file."""
    token = os.environ.get("CLAWOS_TOKEN")
    if token:
        return token

    # Try reading from auth file (saved by the web UI)
    auth_file = os.path.expanduser("~/.clawos/auth.json")
    if os.path.exists(auth_file):
        with open(auth_file) as f:
            data = json.load(f)
            return data.get("token")

    print("Error: No auth token. Set CLAWOS_TOKEN env var or login via UI.", file=sys.stderr)
    sys.exit(1)


def api_call(method, path, data=None):
    """Make an API call and return parsed JSON."""
    url = f"{API_BASE}{path}"
    headers = {
        "Authorization": f"Bearer {get_token()}",
        "Content-Type": "application/json",
    }

    body = json.dumps(data).encode() if data else None
    req = urllib.request.Request(url, data=body, headers=headers, method=method)

    try:
        with urllib.request.urlopen(req) as resp:
            content = resp.read().decode()
            return json.loads(content) if content else None
    except urllib.error.HTTPError as e:
        error_body = e.read().decode()
        try:
            error_json = json.loads(error_body)
            print(f"API Error ({e.code}): {json.dumps(error_json, indent=2)}", file=sys.stderr)
        except json.JSONDecodeError:
            print(f"API Error ({e.code}): {error_body}", file=sys.stderr)
        sys.exit(1)


def list_jobs():
    jobs = api_call("GET", "/cron-job")
    if not jobs:
        print("No cron jobs found.")
        return
    print(f"Found {len(jobs)} cron job(s):\n")
    for job in jobs:
        status = "active" if job.get("isActive") else "inactive"
        schedule = job.get("scheduleJson", "no schedule")
        print(f"  [{status}] {job['name']} (id: {job['id']})")
        if job.get("content"):
            print(f"         content: {job['content'][:80]}...")
        print()


def list_tools():
    tools = api_call("GET", "/cron-job/tools")
    if not tools:
        print("No tools available.")
        return
    print(f"Available tools ({len(tools)}):\n")
    for tool in tools:
        params = tool.get("parameters", {})
        props = params.get("properties", {}) if isinstance(params, dict) else {}
        required = params.get("required", []) if isinstance(params, dict) else []
        param_names = list(props.keys())
        req_str = ", ".join(f"*{p}" if p in required else p for p in param_names)
        print(f"  {tool['name']}: {tool.get('description', '')[:60]}")
        print(f"    params: [{req_str}]")
        print()


def list_tool_instances():
    instances = api_call("GET", "/tool-instance")
    if not instances:
        print("No tool instances found.")
        return
    print(f"Found {len(instances)} tool instance(s):\n")
    for ti in instances:
        print(f"  #{ti['name']} -> {ti.get('toolName', '?')} (id: {ti['id']})")
        if ti.get("description"):
            print(f"    {ti['description']}")
        print()


def list_skills():
    skills = api_call("GET", "/cron-job/skills")
    if not skills:
        print("No skills available.")
        return
    print(f"Available skills ({len(skills)}):\n")
    for skill in skills:
        tool_list = ", ".join(skill.get("tools", []))
        print(f"  @{skill['name']}: {skill.get('description', '')[:60]}")
        if tool_list:
            print(f"    tools: [{tool_list}]")
        print()


def create_job(json_str):
    config = json.loads(json_str)

    # Build the API request
    payload = {
        "name": config["name"],
        "wakeMode": config.get("wakeMode", "Both"),
        "content": config.get("content", ""),
    }

    # Schedule
    if "schedule" in config:
        payload["scheduleJson"] = json.dumps(config["schedule"])

    # Context (skill names -> JSON array)
    if "context" in config:
        ctx = config["context"]
        if isinstance(ctx, list):
            payload["contextJson"] = json.dumps(ctx)
        elif isinstance(ctx, str):
            payload["contextJson"] = json.dumps([ctx])

    result = api_call("POST", "/cron-job", payload)
    print(f"Created cron job: {result['name']} (id: {result['id']})")
    return result


def create_tool_instance(json_str):
    config = json.loads(json_str)

    payload = {
        "name": config["name"],
        "toolName": config["toolName"],
        "argsJson": json.dumps(config.get("args", {})),
        "description": config.get("description", ""),
    }

    result = api_call("POST", "/tool-instance", payload)
    print(f"Created tool instance: #{result['name']} -> {result['toolName']} (id: {result['id']})")
    return result


def execute_job(job_id):
    result = api_call("POST", f"/cron-job/{job_id}/execute")
    print(f"Job execution started: {result.get('executionId', 'unknown')}")
    return result


def delete_job(job_id):
    api_call("DELETE", f"/cron-job/{job_id}")
    print(f"Deleted cron job: {job_id}")


def delete_tool_instance(instance_id):
    api_call("DELETE", f"/tool-instance/{instance_id}")
    print(f"Deleted tool instance: {instance_id}")


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)

    cmd = sys.argv[1]

    if cmd == "list-jobs":
        list_jobs()
    elif cmd == "list-tools":
        list_tools()
    elif cmd == "list-tool-instances":
        list_tool_instances()
    elif cmd == "list-skills":
        list_skills()
    elif cmd == "create-job":
        if len(sys.argv) < 3:
            print("Usage: manage.py create-job '<json>'", file=sys.stderr)
            sys.exit(1)
        create_job(sys.argv[2])
    elif cmd == "create-tool-instance":
        if len(sys.argv) < 3:
            print("Usage: manage.py create-tool-instance '<json>'", file=sys.stderr)
            sys.exit(1)
        create_tool_instance(sys.argv[2])
    elif cmd == "execute-job":
        if len(sys.argv) < 3:
            print("Usage: manage.py execute-job <job-id>", file=sys.stderr)
            sys.exit(1)
        execute_job(sys.argv[2])
    elif cmd == "delete-job":
        if len(sys.argv) < 3:
            print("Usage: manage.py delete-job <job-id>", file=sys.stderr)
            sys.exit(1)
        delete_job(sys.argv[2])
    elif cmd == "delete-tool-instance":
        if len(sys.argv) < 3:
            print("Usage: manage.py delete-tool-instance <instance-id>", file=sys.stderr)
            sys.exit(1)
        delete_tool_instance(sys.argv[2])
    else:
        print(f"Unknown command: {cmd}", file=sys.stderr)
        print(__doc__)
        sys.exit(1)


if __name__ == "__main__":
    main()
