## How to use the JsonGenerators

`JsonGenerators` uses a defined Json Schema and automatically generates the classes present in this file to be used in Bonsai.
For now, add this repo as a `Submodule` to your project. After cloning your repo you might need to initialize the submodule, i.e.:

```cmd
git submodule init
git submodule update
```

After compiling the `.Net` project:

```cmd
cd "JsonGenerators\Generators\JsonSchemaGenerator"
dotnet build
```

you can run the main application executable:

```cmd
"JsonSchemaGenerator\bin\Debug\net472\JsonSchemaGenerator.exe" "Path\To\Schema.json" "output\path\dir" "GeneratedClasses.cs"
```