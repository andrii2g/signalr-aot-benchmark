#!/usr/bin/env python3
import argparse
import json
from datetime import datetime, timezone
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser(description="Generate a SignalR AOT benchmark Markdown report.")
    parser.add_argument("--jit-client", required=True)
    parser.add_argument("--jit-server", required=True)
    parser.add_argument("--aot-client", required=True)
    parser.add_argument("--aot-server", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()

    jit_client = read_json(args.jit_client)
    jit_server = read_json(args.jit_server)
    aot_client = read_json(args.aot_client)
    aot_server = read_json(args.aot_server)

    rows = [
        ("Publish size MB", mb(jit_server["publishSizeBytes"]), mb(aot_server["publishSizeBytes"]), "lower", "{:.1f}"),
        ("Startup ms", jit_server["startupMs"], aot_server["startupMs"], "lower", "{:.0f}"),
        ("Peak RSS MB", kb_to_mb(jit_server["peakRssKb"]), kb_to_mb(aot_server["peakRssKb"]), "lower", "{:.1f}"),
        ("Actual messages sent", jit_client["actualMessagesSent"], aot_client["actualMessagesSent"], "info", "{:,.0f}"),
        ("Actual messages received", jit_client["actualMessagesReceived"], aot_client["actualMessagesReceived"], "higher", "{:,.0f}"),
        ("Receive completeness %", jit_client["receiveCompletenessPercent"], aot_client["receiveCompletenessPercent"], "higher", "{:.2f}"),
        ("Sent messages/sec", jit_client["sentMessagesPerSecond"], aot_client["sentMessagesPerSecond"], "higher", "{:,.1f}"),
        ("Received messages/sec", jit_client["receivedMessagesPerSecond"], aot_client["receivedMessagesPerSecond"], "higher", "{:,.1f}"),
        ("Average latency ms", jit_client["averageLatencyMs"], aot_client["averageLatencyMs"], "lower", "{:.2f}"),
        ("P50 latency ms", jit_client["p50LatencyMs"], aot_client["p50LatencyMs"], "lower", "{:.2f}"),
        ("P95 latency ms", jit_client["p95LatencyMs"], aot_client["p95LatencyMs"], "lower", "{:.2f}"),
        ("P99 latency ms", jit_client["p99LatencyMs"], aot_client["p99LatencyMs"], "lower", "{:.2f}"),
        ("Send errors", jit_client["sendErrors"], aot_client["sendErrors"], "lower", "{:,.0f}"),
    ]

    report = [
        "# SignalR Native AOT Benchmark Report",
        "",
        f"Generated at: {datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace('+00:00', 'Z')}",
        "",
        "## Scenario",
        "",
        f"- Connections: {jit_client['connections']:,}",
        f"- Messages per connection: {jit_client['messagesPerConnection']:,}",
        f"- Target messages sent: {jit_client['targetMessagesSent']:,}",
        f"- Expected broadcast receives: {jit_client['expectedMessagesReceived']:,}",
        "- Transport: WebSockets only",
        "",
        "## Results",
        "",
        "| Metric | JIT | AOT | Better |",
        "|---|---:|---:|---|",
    ]

    for label, jit_value, aot_value, direction, value_format in rows:
        report.append(
            f"| {label} | {format_value(jit_value, value_format)} | {format_value(aot_value, value_format)} | {winner(jit_value, aot_value, direction)} |"
        )

    report.extend(
        [
            "",
            "## Notes",
            "",
            "Native AOT primarily optimizes deployment shape, startup, and memory. Throughput and latency depend heavily on hardware, OS, networking stack, load level, and SignalR workload shape.",
            "",
        ]
    )

    output = Path(args.output)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text("\n".join(report), encoding="utf-8")
    return 0


def read_json(path: str) -> dict:
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def mb(bytes_value: float) -> float:
    return bytes_value / 1024 / 1024


def kb_to_mb(kb_value: float) -> float:
    return kb_value / 1024


def format_value(value: float, value_format: str) -> str:
    return value_format.format(value)


def winner(jit_value: float, aot_value: float, direction: str) -> str:
    if direction == "info":
        return "n/a"

    if nearly_equal(jit_value, aot_value):
        return "tie"

    if direction == "lower":
        return "JIT" if jit_value < aot_value else "AOT"

    return "JIT" if jit_value > aot_value else "AOT"


def nearly_equal(left: float, right: float) -> bool:
    baseline = max(abs(left), abs(right), 1.0)
    return abs(left - right) / baseline < 0.01


if __name__ == "__main__":
    raise SystemExit(main())
