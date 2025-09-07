# EmergencyManagementMcp Integration Tests

This project contains integration tests for the Emergency Management MCP Azure Function application.

## Overview

The integration tests validate various components of the Emergency Management MCP system, including:
- URL construction for external APIs (ArcGIS, Azure Maps)
- Model validation and data structures
- Coordinate transformation and bounding box calculations
- JSON response parsing and extraction
- Fire zone checking and geocoding workflows

## Test Organization

The tests are organized into the following test classes:

### Core Functionality Tests
- **AddressFireZoneCheckTest** - Tests the address fire zone checking workflow
- **StandaloneAddressFireZoneTest** - Validates model structures and data flow
- **CaliforniaBoundingBoxTest** - Tests California coordinate handling and URL construction
- **CoordinateTransformationDebugTest** - Debugs and validates coordinate transformations

### API Integration Tests
- **ArcGisUrlConstructionTest** - Validates ArcGIS API URL construction and parameters
- **RouterClientApiFormatTest** - Tests Azure Maps API request formatting
- **DrivingDirectionsTest** - Tests JSON parsing and driving instruction extraction

## Running the Tests

### Command Line
Run all tests from the solution root:
```bash
dotnet test
```

Run only the integration tests:
```bash
dotnet test tests/EmergencyManagementMcp.IntegrationTests/
```

Run tests with detailed output:
```bash
dotnet test tests/EmergencyManagementMcp.IntegrationTests/ --logger "console;verbosity=detailed"
```

Run a specific test class:
```bash
dotnet test tests/EmergencyManagementMcp.IntegrationTests/ --filter "ClassName=AddressFireZoneCheckTest"
```

Run a specific test method:
```bash
dotnet test tests/EmergencyManagementMcp.IntegrationTests/ --filter "MethodName=TestAddressFireZoneCheckFlow"
```

### Visual Studio / VS Code
- Use the Test Explorer to run individual tests or test classes
- Set breakpoints in test methods for debugging
- Use the integrated terminal to run dotnet test commands

## Test Patterns

### Integration Test Structure
Each test class follows this pattern:
1. **Setup** - Initialize test data and dependencies
2. **Execute** - Run the functionality being tested
3. **Validate** - Assert expected outcomes and log results
4. **Output** - Use ITestOutputHelper for detailed logging

### Test Output
Tests use `ITestOutputHelper` to provide detailed output that helps with:
- Understanding test execution flow
- Debugging API constructions and transformations
- Validating expected vs actual results
- Troubleshooting integration issues

### Assertion Patterns
Tests use xUnit assertions for validation:
- `Assert.True()` / `Assert.False()` for boolean conditions
- `Assert.Equal()` for exact value comparisons
- `Assert.Contains()` / `Assert.DoesNotContain()` for string/collection validation
- `Assert.InRange()` for numeric validation
- `Assert.NotNull()` for object validation

## Dependencies

The test project references:
- **Main project** (`EmergencyManagementMcp`) - Access to models, services, and utilities
- **xUnit** - Test framework
- **Microsoft.Extensions.*** - Configuration, logging, and dependency injection
- **.NET 8** - Target framework

## Best Practices

1. **Descriptive Test Names** - Test method names clearly describe what is being tested
2. **Detailed Output** - Use ITestOutputHelper to provide context and debugging information
3. **Multiple Assertions** - Group related validations in comprehensive test methods
4. **Real-world Data** - Use realistic test data that reflects actual usage scenarios
5. **Error Cases** - Include tests for edge cases and error conditions

## Adding New Tests

When adding new tests:
1. Create a new test class in the appropriate category
2. Follow the existing naming conventions and patterns
3. Include comprehensive output logging
4. Add both positive and negative test cases
5. Update this README if adding new test categories

## Continuous Integration

The tests are designed to:
- Run quickly without external dependencies
- Provide clear failure messages
- Work in both local and CI environments
- Support parallel execution (where appropriate)

For CI/CD pipelines, use:
```bash
dotnet test --configuration Release --logger trx --results-directory TestResults
```