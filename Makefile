CLI     := src/ModularPatchTools.Cli
WEB     := src/ModularPatchTools.Web
CONFIG  := Release
PLUGINS ?= ../Cardinal/plugins

.PHONY: version build test format catalog library-catalog cardinal-catalog web publish-linux publish-win publish-osx publish-web publish-all clean

# The version every binary is built with (Directory.Build.props); a release is the tag v<version>.
version:
	@dotnet msbuild src/ModularPatchTools.Core -getProperty:Version

build:
	dotnet build

test:
	dotnet test

format:
	dotnet format

# Regenerates catalog/ports.json from the Cardinal plugin sources, plus the open-source plugins Cardinal doesn't bundle
# (catalog/extra-sources.txt, fetched into out/extra-plugins at their pinned commits). Rebuild afterwards: it is
# embedded in Core.
catalog:
	scripts/fetch-extra-sources.sh
	dotnet run --project $(CLI) -- catalog build --src $(PLUGINS) --src out/extra-plugins --out catalog/ports.json --report catalog/scan-report.txt
	dotnet build

# Regenerates catalog/library.json: names and tags, from the VCV Library's manifests (VCVRack/library at the pinned
# commit below), of the modules ports.json lacks (closed-source plugins…), and catalog/library-plugins.json: the
# plugins the Library publishes (others get no Library link). Run it after `make catalog`.
LIBRARY_COMMIT := 9c4e6ffe2629d2afdb5b5b0dce838eeac661d783
library-catalog:
	scripts/fetch-library-manifests.sh $(LIBRARY_COMMIT)
	dotnet run --project $(CLI) -- catalog library --manifests out/vcv-library/manifests --out catalog/library.json
	dotnet build

# Regenerates catalog/cardinal.json: modules official Cardinal (DISTRHO upstream/main) loads, and those only the
# neilbgr edition (the Cardinal working copy) adds. Needs the Cardinal clone's "upstream" remote.
CARDINAL ?= $(PLUGINS)/..
cardinal-catalog:
	scripts/export-cardinal-upstream.sh $(CARDINAL) out/cardinal-upstream
	dotnet run --project $(CLI) -- catalog cardinal --upstream out/cardinal-upstream --fork $(CARDINAL) --out catalog/cardinal.json
	dotnet build

# Local dev server for the Blazor app: http://localhost:5016 (add ?sample=AmbientJam to load the sample).
web:
	dotnet run --project $(WEB) --launch-profile http

publish-linux:
	dotnet publish $(CLI) -c $(CONFIG) -r linux-x64 --self-contained -p:PublishSingleFile=true -o publish/linux-x64

publish-win:
	dotnet publish $(CLI) -c $(CONFIG) -r win-x64 --self-contained -p:PublishSingleFile=true -o publish/win-x64

publish-osx:
	dotnet publish $(CLI) -c $(CONFIG) -r osx-x64 --self-contained -p:PublishSingleFile=true -o publish/osx-x64

# Static site in publish/web/wwwroot: can be served by any static file server.
publish-web:
	dotnet publish $(WEB) -c $(CONFIG) -o publish/web

publish-all: publish-linux publish-win publish-web

clean:
	rm -rf publish out
	dotnet clean
