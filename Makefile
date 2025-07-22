vm-start:
	dotnet run --project ./ShpCore.CLI.csproj vm vm-start --image ./Output/alpine-sharpcore.qcow2

vm-test:
	dotnet run --project ./ShpCore.CLI.csproj vm vm-test

test:
	dotnet test ./ShpCore.Kernel.Tests.csproj

build:
	dotnet build ./ShpCore.CLI.csproj

shutdown:
	dotnet run --project ./ShpCore.CLI.csproj vm vm-shutdown
