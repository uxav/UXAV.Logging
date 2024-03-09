# UXAV.Logging

[![GitHub Actions Workflow Status](https://img.shields.io/github/actions/workflow/status/uxav/UXAV.Logging/test.yml?branch=main&style=flat&logo=github&label=status)](https://github.com/uxav/UXAV.Logging/actions)
[![GitHub Issues](https://img.shields.io/github/issues/uxav/UXAV.Logging?style=flat&logo=github)](https://github.com/uxav/UXAV.Logging/issues)
[![Pull Requests](https://img.shields.io/github/issues-pr/uxav/UXAV.Logging?style=flat&logo=github)](https://github.com/uxav/UXAV.Logging/pulls)
[![NuGet Version](https://img.shields.io/nuget/v/UXAV.Logging?style=flat&logo=nuget)](https://www.nuget.org/packages/UXAV.Logging)
[![NuGet Downloads](https://img.shields.io/nuget/dt/UXAV.Logging?style=flat&logo=nuget)](https://www.nuget.org/packages/UXAV.Logging)
[![GitHub License](https://img.shields.io/github/license/uxav/UXAV.Logging?style=flat)](LICENSE)

A Crestron logging solution with built-in console server and custom actions 🙂

## Links

GitHub Repository: [UXAV.Logging](https://github.com/uxav/UXAV.Logging)

NuGet Package: [UXAV.Logging](https://www.nuget.org/packages/UXAV.Logging/)

## Usage

To use this test library in your project, follow these steps:

1. Install the package via NuGet. You can use the following command in the Package Manager Console:

   ```
    dotnet add [<PROJECT>] package UXAV.Logging
   ```

2. Import the library in your code file:

   ```csharp
   using UXAV.Logging;
   ```

3. Start using the library's features in your code:
   ```csharp
   public ControlSystem()
   {
     
   #if DEBUG
       Logger.Level = Logger.LoggerLevel.Debug;
       Logger.DefaultLogStreamLevel = Logger.LoggerLevel.Debug;
   #else
       Logger.Level = Logger.LoggerLevel.Info;
       Logger.DefaultLogStreamLevel = Logger.LoggerLevel.Warning;
   #endif

       Logger.StartConsole((int)(9000 + InitialParametersClass.ApplicationNumber));
       Logger.Log("Hello, world!");
   
   }
   ```

## Documentation

TBC

## Contributing

Contributions are welcome! If you would like to contribute to this project, please follow these guidelines:

1. Fork the repository.
2. Create a new branch for your feature or bug fix.
3. Make your changes and commit them.
4. Push your changes to your forked repository.
5. Submit a pull request to the main repository.

Please ensure that your code follows the project's coding conventions and includes appropriate tests.

- For feature branches use the name `feature/feature-name`
- Version numbers are checked against existing tags and fail CI on match

Thank you for your interest in contributing to this project!

## License

This project is licensed under the [MIT License](LICENSE).
