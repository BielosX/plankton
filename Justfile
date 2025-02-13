[working-directory: 'Plankton.App']
build:
    dotnet build

[working-directory: 'Plankton.App']
run:
    dotnet run

[working-directory: 'Plankton.Tests']
test:
    dotnet test

[working-directory: 'frontend']
frontend-dev:
    npm run dev