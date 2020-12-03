# LINQAnalyzer
![nuget](https://img.shields.io/nuget/v/LINQAnalyzer)![vs marketplace](https://img.shields.io/visual-studio-marketplace/v/LINQAnalyzer.1de9f7e6-2e59-478c-92de-06a761762a9a)

A Diagnostic extension for the C# .NET Compiler Platform ("Roslyn") to analyze LINQ.

# Latest Notes
- The analyzer only looks for expression statements that end with invocations with the IQueryable<T> return type and suggests adding a Select statement that assigns to all properties of T.

---

# Motivation for making the project

Writing LINQ faster - especially with EF/EF Core.
