# Bob tips

Bob tips detects code quality issues in real-time with AI-powered refactoring suggestions for complex functions. Reduce technical debt as you code.

### Why use Bob tips?
* **Real-time code analysis**: Automatically detects quality issues as you write code, without manual scanning.
* **Catch quality issues early**: Identify problematic functions during development, before code review.
* **Reduce technical debt**: Address complexity and maintainability issues as they emerge.
* **Improve code readability**: Get specific suggestions for simplifying complex logic.
* **Enhance testability**: Reduce deeply nested logic and excessive branching that makes testing difficult.

### How it works
Bob tips performs continuous static analysis on all open files in your editor, calculating code quality metrics at the function level:

**Metrics analyzed:**

* **Cyclomatic complexity**: Measures the number of independent paths through your code. A value of 10 or higher is considered high complexity.
* **Maintainability index**: Evaluates how easy your code is to read, test, and extend. A value of 70 or lower is considered low maintainability.

**Automatic detection:**

When a function exceeds quality thresholds, Bob automatically:
* Marks the function with a purple underline in your editor.
* Adds the finding to the Bob Findings panel.
* Generates AI-powered refactoring suggestions.

Bob tips runs continuously in the background without requiring manual scans or configuration. Analysis happens automatically as you write and modify code.

### What Bob tips detects
Bob tips specifically targets two categories of code quality issues:

#### High cyclomatic complexity
Functions with deeply nested logic or excessive branching:
* Multiple nested if/else statements.
* Complex switch statements with many cases.
* Deeply nested loops.
* Excessive conditional logic.

These patterns make code difficult to understand, test, and maintain.

#### Low maintainability
Functions that are difficult to read, test, or extend:
* Long functions with too many responsibilities.
* Poor separation of concerns.
* Complex logic that is hard to follow.
* Code that would benefit from decomposition.

### View and fix findings

#### In the editor
When Bob identifies a quality issue:
1. The problematic function appears with a purple underline in your editor.
2. Hover over the underline to see a tooltip with:
   * The specific quality issue detected.
   * AI-powered refactoring suggestions.
   * Options to discuss the issue with Bob.

#### Using the Bob Findings panel
All findings across your open files are aggregated in the Bob Findings panel:
* View all quality issues in one centralized location.
* Navigate between findings across multiple files.
* Track which issues you have addressed.
* Reference findings in chat using `@problems`.

### Fix with Bob
Collaborate with Bob to refactor your code:
1. Hover over the purple underline in your editor.
2. Click **Fix with Bob** in the tooltip.
3. Bob opens a chat conversation with context about the issue.
4. Discuss the refactoring approach and implement the solution together.

Use this option when you want to:
* Understand the issue in more detail.
* Explore different refactoring approaches.
* Plan a more complex refactoring strategy.
* Learn best practices for similar situations.

### Configure Bob tips

#### Enable or disable the feature
1. Click the **Bob - Settings** button in the bottom right corner of IBM Bob.
2. Click the **Bob findings** tab.
3. Toggle **Bob tips** on or off.

When disabled, Bob stops analyzing files and removes existing findings from the editor and Bob Findings panel.
