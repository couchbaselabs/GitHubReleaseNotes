#!/bin/sh

abort()
{
      echo "Operation cancelled."
      exit 99
}

# Stop on first error.
set -e

# Ensure Github credentials are provided as environment variables.
if [[ -z "${GITHUB_USERNAME}" ]] || [[ -z "${GITHUB_API_KEY}" ]]; then
      echo "You must provide your Github credentials via the GITHUB_USERNAME and GITHUB_API_KEY environment variables."
      echo "If you have enabled Github's 2-factor authentication, then you will need to provide a personal access token instead."
      echo "Visit https://github.com/settings/tokens/ to create one, or for more details."
      abort
fi

# Pass in a valid command, such as 'create' or 'update'
COMMAND=$1
VERSION=$2

if [[ "${COMMAND}" != "create" ]] && [[ "${COMMAND}" != "update" ]] && [[ -z $VERSION ]]; then
      echo "Valid commands are:"      
      echo " 'create' - Creates a new Github release. Takes one argument: the verson string (e.g. '1.2.1'). Assumes the desired release branch is named 'release/{version}'."
      echo " 'update' - Updates an existing Github release. Takes one argument: the verson string (e.g. '1.2.1')."
      abort
fi

# Pass in a version number argument
if [[ -z $VERSION ]]; then
      echo "Missing the desired version number. The '$COMMAND' command requires this parameter."      
      abort
fi

echo ">>> Running command: '$COMMAND' release '$VERSION', against the following repos:"

echo '\tcouchbase-lite-net'
mono bin/Release/ReleaseNotesCompiler.CLI.exe $COMMAND --exportxml --owner couchbase --repository couchbase-lite-net --targetcommitish release/$VERSION -u $GITHUB_USERNAME -p $GITHUB_API_KEY -m $VERSION
echo '\tcouchbase-lite-ios'
mono bin/Release/ReleaseNotesCompiler.CLI.exe $COMMAND --exportxml --owner couchbase --repository couchbase-lite-ios --targetcommitish release/$VERSION -u $GITHUB_USERNAME -p $GITHUB_API_KEY -m $VERSION
echo '\tcouchbase-lite-java-core'
mono bin/Release/ReleaseNotesCompiler.CLI.exe $COMMAND --exportxml --owner couchbase --repository couchbase-lite-java-core --targetcommitish release/$VERSION -u $GITHUB_USERNAME -p $GITHUB_API_KEY -m $VERSION
echo '\tcouchbase-lite-java-listener'
mono bin/Release/ReleaseNotesCompiler.CLI.exe $COMMAND --exportxml --owner couchbase --repository couchbase-lite-java-listener --targetcommitish master -u $GITHUB_USERNAME -p $GITHUB_API_KEY -m $VERSION
echo '\tcouchbase-lite-java'
mono bin/Release/ReleaseNotesCompiler.CLI.exe $COMMAND --exportxml --owner couchbase --repository couchbase-lite-java --targetcommitish release/$VERSION -u $GITHUB_USERNAME -p $GITHUB_API_KEY -m $VERSION
echo '\tcouchbase-lite-android'
mono bin/Release/ReleaseNotesCompiler.CLI.exe $COMMAND --exportxml --owner couchbase --repository couchbase-lite-android --targetcommitish release/$VERSION -u $GITHUB_USERNAME -p $GITHUB_API_KEY -m $VERSION
echo '\tsync_gateway'
mono bin/Release/ReleaseNotesCompiler.CLI.exe $COMMAND --exportxml --owner couchbase --repository sync_gateway --targetcommitish release/$VERSION -u $GITHUB_USERNAME -p $GITHUB_API_KEY -m $VERSION

echo 'Done!'
echo