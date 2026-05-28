#!/usr/bin/env python3

import sys
import xml.etree.ElementTree as ET


def main() -> int:
    if len(sys.argv) != 3:
        print("usage: check_cobertura_threshold.py <coverage.xml> <min-percent>", file=sys.stderr)
        return 2

    coverage_file = sys.argv[1]
    minimum = float(sys.argv[2])

    root = ET.parse(coverage_file).getroot()
    line_rate = root.attrib.get("line-rate")
    if line_rate is None:
        print(f"missing line-rate in {coverage_file}", file=sys.stderr)
        return 2

    percent = float(line_rate) * 100.0
    print(f"coverage: {percent:.2f}%")

    if percent + 1e-9 < minimum:
        print(f"coverage below threshold: required {minimum:.2f}%", file=sys.stderr)
        return 1

    print(f"coverage meets threshold: required {minimum:.2f}%")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
