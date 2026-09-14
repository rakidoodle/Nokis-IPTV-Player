#!/bin/sh
set -eu
cd "$(dirname "$0")/.."
if [ -z "${JAVA_HOME:-}" ] && [ -d .tools/amazon-corretto-17.jdk/Contents/Home ]; then export JAVA_HOME="$PWD/.tools/amazon-corretto-17.jdk/Contents/Home"; fi
if [ -x ./gradlew ]; then exec ./gradlew "$@"; fi
exec .tools/gradle-8.11.1/bin/gradle "$@"
