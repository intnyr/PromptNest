# AI Prompt Library

This library contains the deduplicated, normalized AI prompts found in this repository. Redundant variants, smoke-test copies, and placeholder-only test strings were consolidated or omitted when they did not add reusable intent.

## Research And Strategy

### Market Analysis - Industry Overview

Sources consolidated: `src/PromptNest.App/ViewModels/LibraryDesignData.cs`, `tests/PromptNest.Data.Tests/RepositoryTests.cs`, `tests/PromptNest.SmokeTests/PromptNestDesktopSmokeTests.cs`, `tests/PromptNest.UiTests/MainViewModelWorkflowTests.cs`.

```prompt
You are a market research analyst.

Provide a comprehensive overview of the {{industry}} industry, including key trends, market size, growth drivers, major players, opportunities, challenges, and future outlook.

Focus on actionable insights for business stakeholders.

Tone: {{tone|friendly}}
Audience: {{audience}}

Format the response as a structured report with clear sections and bullet points where appropriate.
```

### User Persona Generator

Source: `src/PromptNest.App/ViewModels/LibraryDesignData.cs`.

```prompt
Create a detailed user persona for {{audience}} based on the following information.

Include demographics, goals, needs, pain points, motivations, behaviors, and any relevant context that would help a product, UX, or marketing team understand this audience.
```

### Competitive Analysis Framework

Source: `src/PromptNest.App/ViewModels/LibraryDesignData.cs`.

```prompt
Analyze the following competitors: {{competitors}}.

Evaluate their strengths, weaknesses, opportunities, and threats. Highlight positioning differences, likely strategic advantages, visible gaps, and practical takeaways.
```

### Topic Review

Source: `tests/PromptNest.Core.Tests/RepositoryPromptImportTests.cs`.

```prompt
Review {{topic}} with care.
```

## Content And Marketing

### Blog Post - How-To Guide

Source: `src/PromptNest.App/ViewModels/LibraryDesignData.cs`.

```prompt
Write a comprehensive how-to guide about {{topic}}.

Include an introduction, step-by-step instructions, practical tips, and a clear conclusion.
```

### Benefit-Focused Product Description

Source: `src/PromptNest.App/ViewModels/LibraryDesignData.cs`.

```prompt
Write a compelling product description for {{product}} that emphasizes the benefits and value for {{audience}}.
```

## Engineering

### Code Review Checklist

Source: `src/PromptNest.App/ViewModels/LibraryDesignData.cs`.

```prompt
Generate a comprehensive code review checklist for {{language}} projects.

Include security, performance, maintainability, readability, testing, and reliability considerations.
```

### Pull Request Review

Sources consolidated: `tests/PromptNest.Core.Tests/RepositoryPromptImportTests.cs`, `docs/repository-prompt-import-contract.md`.

```prompt
Review this pull request for correctness, missing tests, and potential regressions.
```

### Repository Architecture Summary

Source: `tests/PromptNest.Core.Tests/RepositoryPromptImportTests.cs`.

```prompt
Summarize the repository architecture for a new contributor.
```

### Commit Message

Source: `tests/PromptNest.Cli.Tests/CliWorkflowTests.cs`.

```prompt
Write a concise commit message for the staged changes.
```

## Communication And Tone

### Tone Brief

Source: `tests/PromptNest.Core.Tests/VariableResolverTests.cs`.

```prompt
Use a {{tone|friendly}} tone for {{audience}} in the {{industry}} industry.
```
