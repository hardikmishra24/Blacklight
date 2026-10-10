dotnet new sln -n TipShield
dotnet new classlib -n TipShield.Core -o src/TipShield.Core --framework net8.0
dotnet new classlib -n TipShield.Gemma -o src/TipShield.Gemma --framework net8.0
dotnet new web -n TipShield.Api -o src/TipShield.Api --framework net8.0
dotnet new blazor -n TipShield.Web -o src/TipShield.Web --framework net8.0
dotnet new worker -n TipShield.Bot -o src/TipShield.Bot --framework net8.0
dotnet new xunit -n TipShield.Core.Tests -o tests/TipShield.Core.Tests --framework net8.0
dotnet new xunit -n TipShield.Gemma.Tests -o tests/TipShield.Gemma.Tests --framework net8.0
dotnet new xunit -n TipShield.Api.Tests -o tests/TipShield.Api.Tests --framework net8.0

dotnet sln add src/TipShield.Core/TipShield.Core.csproj
dotnet sln add src/TipShield.Gemma/TipShield.Gemma.csproj
dotnet sln add src/TipShield.Api/TipShield.Api.csproj
dotnet sln add src/TipShield.Web/TipShield.Web.csproj
dotnet sln add src/TipShield.Bot/TipShield.Bot.csproj
dotnet sln add tests/TipShield.Core.Tests/TipShield.Core.Tests.csproj
dotnet sln add tests/TipShield.Gemma.Tests/TipShield.Gemma.Tests.csproj
dotnet sln add tests/TipShield.Api.Tests/TipShield.Api.Tests.csproj

dotnet add src/TipShield.Gemma/TipShield.Gemma.csproj reference src/TipShield.Core/TipShield.Core.csproj
dotnet add src/TipShield.Api/TipShield.Api.csproj reference src/TipShield.Core/TipShield.Core.csproj
dotnet add src/TipShield.Api/TipShield.Api.csproj reference src/TipShield.Gemma/TipShield.Gemma.csproj
dotnet add src/TipShield.Web/TipShield.Web.csproj reference src/TipShield.Core/TipShield.Core.csproj
dotnet add src/TipShield.Bot/TipShield.Bot.csproj reference src/TipShield.Core/TipShield.Core.csproj
dotnet add tests/TipShield.Core.Tests/TipShield.Core.Tests.csproj reference src/TipShield.Core/TipShield.Core.csproj
dotnet add tests/TipShield.Gemma.Tests/TipShield.Gemma.Tests.csproj reference src/TipShield.Core/TipShield.Core.csproj
dotnet add tests/TipShield.Gemma.Tests/TipShield.Gemma.Tests.csproj reference src/TipShield.Gemma/TipShield.Gemma.csproj
dotnet add tests/TipShield.Api.Tests/TipShield.Api.Tests.csproj reference src/TipShield.Api/TipShield.Api.csproj
dotnet add tests/TipShield.Api.Tests/TipShield.Api.Tests.csproj reference src/TipShield.Core/TipShield.Core.csproj
dotnet add tests/TipShield.Api.Tests/TipShield.Api.Tests.csproj reference src/TipShield.Gemma/TipShield.Gemma.csproj

dotnet build TipShield.sln
