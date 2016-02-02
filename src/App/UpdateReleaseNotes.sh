#!/bin/sh
mono bin/Release/ReleaseNotesCompiler.CLI.exe update --owner couchbase --repository couchbase-lite-net --targetcommitish release/1.2 -u USER -p PASSWORD -m 1.2
mono bin/Release/ReleaseNotesCompiler.CLI.exe update --owner couchbase --repository couchbase-lite-ios --targetcommitish release/1.2.0 -u USER -p PASSWORD -m 1.2
mono bin/Release/ReleaseNotesCompiler.CLI.exe update --owner couchbase --repository couchbase-lite-java-core --targetcommitish release/1.2.0 -u USER -p PASSWORD -m 1.2.0
mono bin/Release/ReleaseNotesCompiler.CLI.exe update --owner couchbase --repository couchbase-lite-java --targetcommitish release/1.2.0 -u USER -p PASSWORD -m 1.2.0
mono bin/Release/ReleaseNotesCompiler.CLI.exe update --owner couchbase --repository couchbase-lite-android --targetcommitish release/1.2.0 -u USER -p PASSWORD -m 1.2.0
mono bin/Release/ReleaseNotesCompiler.CLI.exe update --owner couchbase --repository sync_gateway --targetcommitish master -u USER -p PASSWORD -m 1.2.0
