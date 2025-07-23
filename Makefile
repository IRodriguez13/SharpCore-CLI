# Makefile para SharpCore CLI

.PHONY: build run vm-start test doctor

build:
	dotnet build ./ShpCore.CLI.csproj 

run:
	dotnet run --project ./ShpCore.CLI.csproj

vm-start:
	dotnet run --project ./ShpCore.CLI.csproj -- vm vm-start --image /home/ivanr013/Escritorio/dev/SSL-Environment/Output/alpine-sharpcore.qcow2 --qemu-options qemu-options-default.json 

vm-init:
	dotnet run --project ./ShpCore.CLI.csproj -- vm vm-init --image /home/ivanr013/Escritorio/dev/SSL-Environment/Output/alpine-sharpcore.qcow2

vm-start--silent:
	dotnet run --project ./ShpCore.CLI.csproj -- vm vm-start --image /home/ivanr013/Escritorio/dev/SSL-Environment/Output/alpine-sharpcore.qcow2 --silent


test:
	dotnet test

doctor:
	dotnet run --project ./ShpCore.CLI.csproj --doctor

