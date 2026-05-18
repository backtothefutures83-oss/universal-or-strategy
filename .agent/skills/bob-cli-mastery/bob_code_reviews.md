# Code reviews

Get AI-powered code reviews directly in your IDE. Bob analyzes changes, validates issue coverage, and flags potential issues before you commit your work.

### Why use code reviews?
* **Catch errors early**: Identify issues before they make it into your committed work.
* **Save time**: Address potential reviewer comments proactively, reducing PR approval time.
* **Improve code quality**: Get suggestions for better coding practices and maintainability.
* **Validate against issues**: Ensure your changes align with issue requirements (GitHub required).

### How it works
Bob provides a dedicated Review Panel in your IDE's sidebar where you can configure and initiate code reviews. The panel offers two review modes:

1. **Branch Comparison**: Compare your changes against any branch.
2. **Issue Coverage**: Validate your local changes against a specific GitHub issue.

Bob analyzes your changes and flags potential issues in the Bob Findings panel, which provides:
* **Hierarchical file lists**: Navigate large changesets more easily with organized file structures.
* **Remote branch support**: Review work from any branch, not just your current one.
* **Issue alignment validation**: Verify that your changes match the intent and requirements of an issue (GitHub required).
* **Configurable exclusions**: Exclude specific files or patterns from reviews.

### Review your code
#### Prerequisites
* You have uncommitted changes in your workspace.
* Access to remote or local branches you want to review.
* For issue coverage validation: A GitHub account and issue URL (GitHub is required for issue-related features).

Bob can work with GitHub and GitLab for pull request workflows and branch comparisons. However, issue coverage validation requires GitHub.

### Initiate a review
You can initiate a code review using the Review Panel or chat commands.

#### Using the Review Panel
Open the Bob Review panel from your sidebar. The panel provides a visual interface with two review modes:

**Branch:**
* Select Branch at the top of the panel.
* Choose a branch to compare against from the dropdown.
* Toggle Include Uncommitted Changes to include local modifications for non-current branches.
* Click Start Review to begin the review.
* Bob reviews your changes and displays findings in the Bob Findings panel.

**Issue:**
* Select Issue mode at the top of the panel.
* Choose a GitHub issue from the dropdown, or select Other to enter a custom issue URL.
* Click Start Review to review your changes against the issue.
* Bob analyzes whether your local changes align with the issue's requirements.

#### Using chat commands
You can initiate reviews directly through the chat interface using slash commands. This is a faster, keyboard-driven alternative to the Review Panel:

**Branch comparison:**
* `/review` - Review uncommitted changes in your working directory.
* `/review <branch>` - Compare your changes against a specific branch.

**Issue coverage:**
* `/review <issue-url> --issue-coverage` - Validate changes against a GitHub issue URL.
* Example: `/review https://github.com/owner/repo/issues/123 --issue-coverage`

### Review options
#### Select a comparison branch
The branch dropdown selector shows:
* **Current branch**: Your active branch with uncommitted changes.
* **Default branch**: Your repository's default branch (marked with a badge).
* **Remote branches**: Branches from your remote repository (marked with a badge).
* **Local branches**: Other local branches (marked with a badge).

#### Show uncommitted changes
When reviewing against a branch other than your current branch, enable the Include Uncommitted Changes checkbox to include uncommitted changes in your review. This option is automatically enabled when reviewing your current branch.

#### Configure review exclusions
You can exclude specific files or patterns from code reviews:
1. Open Bob Settings.
2. Navigate to the Bob Findings tab.
3. Under Review Exclusions, add glob patterns for files to exclude (e.g., `.vscode/**`, `*.test.ts`).
4. Excluded files will not appear in the review panel's file list and will not be analyzed during reviews.

#### Auto-approval during reviews
You can enable auto-approval for review actions to keep your workflow moving. This allows Bob to proceed with review-related actions without requiring manual approval for each step.

The review process uses both read tools for analyzing code and write tools for submitting findings. To enable full auto-approval for reviews:
* Enable Always allow read tools for code analysis actions.
* Enable Always allow write tools for submitting review findings to the Bob Findings panel.

### Using Bob Findings
Once Bob completes a review, findings appear in the Bob Findings panel. You can interact with these findings in several ways:

#### Viewing findings
* Click on any finding in the Bob Findings panel to see full details.
* Use `@issues` in the chat interface context dropdown menu to reference findings in conversation.
* Navigate to the file by clicking on the finding to see the code in context with inline annotations.

#### Taking action on findings
Each finding provides several action buttons:
* **Fix with Bob**: Ask Bob to automatically fix the issue. Bob will create a task to address the finding.
* **Mark as Resolved**: Mark the finding as resolved if you've addressed it manually.
* **Mark as Open**: Reopen a previously resolved finding.
* **Dismiss**: Remove the finding from the panel if it's not relevant.

#### Finding status tracking
Findings can have three statuses:
* **Open**: Issue identified and needs attention.
* **In Progress**: Bob is actively working on fixing the issue.
* **Resolved**: Issue has been addressed.

### Tips and best practices
* **Combine with manual reviews**: Use Bob's automated reviews as a first pass before human code reviews.
* **Review before committing**: Run reviews on uncommitted changes to catch issues early.
* **Validate issue coverage**: Use Issue mode to ensure your changes fully address the requirements before creating a pull request.
* **Configure exclusions**: Exclude test files, generated code, or configuration files that don't need review.
* **Compare against remote branches**: Use the branch selector to review your work against the latest remote changes.
