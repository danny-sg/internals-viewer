# Copilot Instructions

## General
- Target `.NET 10` and use the language version configured by the project.
- Always use braces for control blocks (`if`, `for`, `foreach`, `while`, `switch` cases with blocks).
- Keep lines at 140 characters or less.
- Prefer properties over fields.
- Prefer private properties over private fields.
- Prefer primary constructors for classes/records when they improve clarity.
- Break long parameter lists onto multiple lines using consistent indentation.
- When implementing changes, do not add explanatory comments in code.

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
1. Constants and `static readonly` fields
2. Properties (use private properties in place of private fields)
3. Constructors
4. Public methods
5. Protected methods
6. Private methods

Static members are not a separate group. A static member belongs to the group its accessibility puts it in,
alongside the instance members of the same visibility - a `private static` helper sits with the other private
methods, not above the constructor. SA1204 is disabled for this reason.

Where a group mixes accessibility, order it most accessible first (`public`, `internal`, `private`).

## Types and Performance
- Performance and memory efficiency are critical.
- Avoid boxing/unboxing.
- Avoid `object` where practical; prefer specific and generic types to preserve compile-time type information.

## Additional
- Do not add README/Markdown files unless explicitly requested.