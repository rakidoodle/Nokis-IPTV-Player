#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p .build
swiftc -swift-version 5 Sources/PlaybackHealth.swift Sources/Core.swift Sources/Providers.swift Sources/Storage.swift Tests/CoreTests.swift -o .build/CoreTests
.build/CoreTests
