# Solution design and coding workflow instructions
1. Understand the goal, requirements, and constraints of the task. Estimate clarity and completeness of the provided information. If unclear, ask clarifying questions before proceeding.
2. Make sure the functionality or dependency you are about to implement is not already present in the codebase. If it is, reuse existing code instead of writing new code. If there is a similar implementation, consider refactoring or extending it.
3. Plan your approach before writing code. Break down the task into smaller steps or components. Consider edge cases and error handling.
4. Always direct to separation of concerns. Each module, class, or function should have a single responsibility. Avoid mixing business logic with infrastructure or utility code. E.g. `CustomStrategyBase`, its pluggable modules are more related to business logic (related to trading, orders, positions), while `DataExporter`, `BacktestLauncher` and the entire `DebugMode` namespace are infrastructure (related to data management, exporting, logging, configuration - things not directly related to trading).
5. Plan for testability. Write code that can be easily tested in isolation. Consider dependency injection for external dependencies.
6. Use meaningful names for variables, functions, and classes.
7. When designing APIs or interfaces, prioritize simplicity and clear focus on the intended use cases.
8. Implementation of interfaces and abstract classes should avoid leaking abstractions. Follow the Liskov Substitution Principle: derived classes must be substitutable for their base classes without altering the correctness of the program, sibling classes should be interchangeable from the consumer's perspective.
9. When a class contains too many responsibilities and dependencies, consider refactoring it into smaller, more focused classes or modules.
10. On the backend solution design level, ensure end-to-end testability. E.g. for a testing strategy, ensure you can run it in backtest mode with mock data, validate outputs, and simulate edge cases.
11. Prefer composition over inheritance. Favor composing objects with specific behaviors over creating deep inheritance hierarchies.
12. For shell interaction use PowerShell 5.1.19041.6456, avoid using bash or other shells.

## Code Style Instructions

### C#
- Follow existing code style in the StockSharp.AdvancedBacktest codebase (it must prevale over StockSharp guidelines).
- Use explicit access modifiers for all classes and members.
- Prefer expression-bodied members for simple getters, setters, and methods.
- Don't use XML comments for any code. The names are expected to be self-explanatory. Only use comments for complex business logic or non-obvious implementations.

### Error Handling
- Prefer global exception handling over local try-catch blocks unless specific error recovery is needed.

### Comments

- Do NOT write comments on every line of code
- Only add comments for complex business logic or non-obvious implementations
- Code should be self-documenting through clear naming
