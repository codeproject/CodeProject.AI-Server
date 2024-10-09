To control when a GitHub Action runs, you can configure the triggers in the workflow file. GitHub Actions can be triggered by various events like pushes, pull requests, manual invocation, schedules, and more. You can choose the events that suit your workflow. Here’s how you can configure the action to run only under specific conditions:

### Key Triggers to Control When a GitHub Action Runs:

1. **Manual Trigger (`workflow_dispatch`)**:
   - This allows you to run the action manually from the GitHub Actions tab, instead of running automatically on every push.

   ```yaml
   on:
     workflow_dispatch:
   ```

2. **Push Events on Specific Branches**:
   - If you only want the action to run on specific branches (e.g., `main`), you can configure the action to only run on pushes to those branches.

   ```yaml
   on:
     push:
       branches:
         - main  # Only run on pushes to the 'main' branch
   ```

3. **Pull Request Events**:
   - You can trigger the action to run only when a pull request is opened, updated, or merged.

   ```yaml
   on:
     pull_request:
       branches:
         - main  # Only trigger on pull requests to 'main' branch
   ```

4. **Run on Specific Files**:
   - If you only want the action to run when certain files change (e.g., files in a `docs/` folder), you can use the `paths` filter:

   ```yaml
   on:
     push:
       branches:
         - main
       paths:
         - 'docs/**'  # Only run when files in the 'docs/' directory change
   ```

5. **Scheduled Runs (`schedule`)**:
   - You can schedule the action to run periodically (e.g., daily or weekly) using cron syntax.

   ```yaml
   on:
     schedule:
       - cron: '0 0 * * 1'  # Runs every Monday at midnight UTC
   ```

6. **Tag or Release Events**:
   - You can trigger actions based on new tags or releases in the repository:

   ```yaml
   on:
     push:
       tags:
         - 'v*'  # Trigger when pushing a version tag like 'v1.0.0'
     release:
       types: [published]  # Trigger on publishing a release
   ```

### Combining Multiple Triggers

You can also combine multiple triggers if you want the action to run under different conditions, like on manual dispatch or when a tag is pushed.

```yaml
on:
  push:
    branches:
      - main
  workflow_dispatch:
  pull_request:
    branches:
      - main
  release:
    types: [published]
```

### Conditional Execution with `if`

In addition to controlling when the action is triggered, you can add conditions to individual steps to control whether they run based on specific conditions like commit messages, file changes, or environment variables.

Example:

```yaml
jobs:
  example_job:
    runs-on: ubuntu-latest
    steps:
      - name: Run only if a specific file changed
        if: contains(github.event.head_commit.message, 'Update README')
        run: echo "README file updated"
```

### Summary

- **Manual trigger**: Use `workflow_dispatch` to trigger manually.
- **Branch-specific**: Use `on: push` with branch filters to trigger actions only on specific branches.
- **Pull request**: Use `on: pull_request` to run the action when a PR is opened or updated.
- **File-specific**: Use `paths` to limit actions to specific files or directories.
- **Scheduled**: Use `schedule` for periodic runs.
- **Tags/releases**: Use `push: tags` or `release` events for versioned workflows.

By combining these methods, you can control precisely when a GitHub Action runs, avoiding unnecessary executions on every push.