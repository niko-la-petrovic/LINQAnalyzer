# LINQAnalyzer
![nuget](https://img.shields.io/nuget/v/LINQAnalyzer)
[![Build and test master](https://github.com/niko-la-petrovic/LINQAnalyzer/actions/workflows/msbuild.yml/badge.svg?branch=master)](https://github.com/niko-la-petrovic/LINQAnalyzer/actions/workflows/msbuild.yml)
[![Build and test develop](https://github.com/niko-la-petrovic/LINQAnalyzer/actions/workflows/dotnet.yml/badge.svg?branch=develop)](https://github.com/niko-la-petrovic/LINQAnalyzer/actions/workflows/dotnet.yml)

A Diagnostic extension for the C# .NET Compiler Platform ("Roslyn") to analyze LINQ.

[NuGet Package](https://www.nuget.org/packages/LINQAnalyzer/)

[Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=niko-la-petrovic.LINQAnalyzer)

![LINQAnalyzer](https://user-images.githubusercontent.com/23142144/136340942-29ba9b56-c6a1-4090-9f7b-52dd76e0ddb3.gif)

# Latest Notes
- The analyzer only looks for expression statements that end with invocations with the IQueryable<T> return type and suggests adding a Select statement that assigns to all properties of T.

---

# Motivation for making the project

Writing LINQ faster - especially with EF/EF Core.
