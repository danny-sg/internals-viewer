# C# Rules

## General
- Target `.NET 10` and use the language version configured by the project.
- Always use braces for control blocks (`if`, `for`, `foreach`, `while`, `switch` cases with blocks).
- Keep lines at 110 characters or less.
- Prefer properties over fields.
- Prefer private properties over private fields.
- Prefer primary constructors for classes/records when they improve clarity.
- Break long parameter lists onto multiple lines using consistent indentation.

## Naming
- Do not use abbreviations; keep names concise and clear.
- Use short lambda parameter names based on the source type (`f`, `fr`, etc.).
- Use `i` or `index` for loop indexes.

## Comments and Documentation
- Do not add inline comments inside methods unless explicitly requested.
- Use XML comments for classes/records and public methods.
- Do not add XML parameter descriptions unless requested.

## Structure
Order members as:
1. Properties/private class variables
2. Constructors
3. Public methods
4. Protected methods
5. Private methods

## Types and Performance
- Performance and memory efficiency are critical.
- Avoid boxing/unboxing.
- Avoid `object` where practical; prefer specific and generic types to preserve compile-time type information.

## Additional
- Do not add README/Markdown files unless explicitly requested.