namespace PromptNest.App.ViewModels;

public static class LibraryDesignData
{
    public static LibraryViewState Create()
    {
        var selectedTags = new[]
        {
            new TagChipViewModel { Name = "research", Color = "#24473f", IsRemovable = true },
            new TagChipViewModel { Name = "market", Color = "#16446e", IsRemovable = true },
            new TagChipViewModel { Name = "analysis", Color = "#153f73", IsRemovable = true }
        };

        var prompts = new[]
        {
            new PromptListItemViewModel
            {
                Id = "market-analysis",
                Title = "Market Analysis - Industry Overview",
                Preview = "You are a market research analyst. Provide a comprehensive overview of the {{industry}} industry including key trends...",
                Date = new DateOnly(2025, 5, 15),
                IsFavorite = true,
                IsSelected = true,
                Tags = selectedTags
            },
            new PromptListItemViewModel
            {
                Id = "user-persona",
                Title = "User Persona Generator",
                Preview = "Create a detailed user persona for {{audience}} based on the following information. Include demographics, goals...",
                Date = new DateOnly(2025, 5, 14),
                Tags =
                [
                    new TagChipViewModel { Name = "persona", Color = "#213b5f" },
                    new TagChipViewModel { Name = "ux", Color = "#16446e" },
                    new TagChipViewModel { Name = "research", Color = "#2f2f6f" }
                ]
            },
            new PromptListItemViewModel
            {
                Id = "competitive-analysis",
                Title = "Competitive Analysis Framework",
                Preview = "Analyze the following competitors: {{competitors}}. Evaluate their strengths, weaknesses, opportunities, and threats...",
                Date = new DateOnly(2025, 5, 13),
                Tags =
                [
                    new TagChipViewModel { Name = "analysis", Color = "#164f55" },
                    new TagChipViewModel { Name = "strategy", Color = "#2d3275" },
                    new TagChipViewModel { Name = "competitive", Color = "#303173" }
                ]
            },
            new PromptListItemViewModel
            {
                Id = "blog-post",
                Title = "Blog Post - How To Guide",
                Preview = "Write a comprehensive how-to guide about {{topic}}. Include introduction, step-by-step instructions, tips, and conclusion...",
                Date = new DateOnly(2025, 5, 12),
                Tags =
                [
                    new TagChipViewModel { Name = "content", Color = "#264b37" },
                    new TagChipViewModel { Name = "blog", Color = "#17404b" },
                    new TagChipViewModel { Name = "how-to", Color = "#303173" }
                ]
            },
            new PromptListItemViewModel
            {
                Id = "product-description",
                Title = "Product Description - Benefit Focused",
                Preview = "Write a compelling product description for {{product}} that emphasizes the benefits and value for {{audience}}...",
                Date = new DateOnly(2025, 5, 11),
                Tags =
                [
                    new TagChipViewModel { Name = "copy", Color = "#49315f" },
                    new TagChipViewModel { Name = "product", Color = "#213b5f" },
                    new TagChipViewModel { Name = "marketing", Color = "#243879" }
                ]
            },
            new PromptListItemViewModel
            {
                Id = "code-review",
                Title = "Code Review Checklist",
                Preview = "Generate a comprehensive code review checklist for {{language}} projects. Include security, performance, maintainability...",
                Date = new DateOnly(2025, 5, 10),
                Tags =
                [
                    new TagChipViewModel { Name = "development", Color = "#264b37" },
                    new TagChipViewModel { Name = "code", Color = "#16446e" },
                    new TagChipViewModel { Name = "quality", Color = "#49315f" }
                ]
            }
        };

        FolderNodeViewModel[] folders =
        [
            new FolderNodeViewModel { Id = "00-inbox", Name = "00_Inbox", Count = 412 },
            new FolderNodeViewModel
            {
                Id = "01-research",
                Name = "01_Research",
                Count = 8732,
                IsExpanded = true,
                Children =
                [
                    new FolderNodeViewModel { Id = "market-research", Name = "Market Research", Count = 2104, IsSelected = true },
                    new FolderNodeViewModel { Id = "user-research", Name = "User Research", Count = 1982 },
                    new FolderNodeViewModel { Id = "competitor-analysis", Name = "Competitor Analysis", Count = 1317 },
                    new FolderNodeViewModel { Id = "reports", Name = "Reports", Count = 3329 }
                ]
            },
            new FolderNodeViewModel { Id = "02-content", Name = "02_Content", Count = 24531 },
            new FolderNodeViewModel { Id = "03-design", Name = "03_Design", Count = 12843 },
            new FolderNodeViewModel { Id = "04-development", Name = "04_Development", Count = 18992 },
            new FolderNodeViewModel { Id = "05-marketing", Name = "05_Marketing", Count = 14552 },
            new FolderNodeViewModel { Id = "06-support", Name = "06_Support", Count = 8114 },
            new FolderNodeViewModel { Id = "07-personal", Name = "07_Personal", Count = 3256 },
            new FolderNodeViewModel { Id = "archive", Name = "Archive", Count = 2513 },
            new FolderNodeViewModel { Id = "templates", Name = "Templates", Count = 1010 }
        ];

        return new LibraryViewState
        {
            NavigationItems =
            [
                new NavigationItemViewModel { Id = "home", Label = "Home", Kind = NavigationItemKind.Home },
                new NavigationItemViewModel { Id = "all", Label = "All Prompts", Kind = NavigationItemKind.AllPrompts, Count = 100248, IsSelected = true },
                new NavigationItemViewModel { Id = "starred", Label = "Starred", Kind = NavigationItemKind.Starred, Count = 1284 },
                new NavigationItemViewModel { Id = "recent", Label = "Recent", Kind = NavigationItemKind.Recent, Count = 342 },
                new NavigationItemViewModel { Id = "trash", Label = "Trash", Kind = NavigationItemKind.Trash, Count = 96 }
            ],
            FolderTree = folders,
            VisibleFolders = MainViewModel.FlattenFolders(folders),
            Collections =
            [
                new NavigationItemViewModel { Id = "shared", Label = "Shared Prompts", Kind = NavigationItemKind.Collection, Count = 1203 }
            ],
            ActiveQuery = new LibraryQueryState
            {
                Scope = LibraryQueryScope.Folder,
                Id = "market-research",
                Label = "Market Research"
            },
            PromptList = prompts,
            SelectedPrompt = new PromptDetailViewModel
            {
                Id = "market-analysis",
                Title = "Market Analysis - Industry Overview",
                FolderId = "market-research",
                FolderPath = "01_Research / Market Research",
                Tags = selectedTags,
                Body = """
                    You are a market research analyst.
                    Provide a comprehensive overview of the {{industry}} industry including key trends, market size, growth drivers, major players, opportunities, challenges, and future outlook.

                    Focus on delivering actionable insights for business stakeholders.

                    Tone: {{tone|friendly}}
                    Audience: {{audience}}

                    Format as a structured report with clear sections and bullet points where appropriate.
                    """,
                Variables =
                [
                    new VariableRowViewModel { Name = "industry", DefaultValue = "SaaS", PreviewValue = "SaaS" },
                    new VariableRowViewModel { Name = "audience", PreviewValue = "Business executives" }
                ],
                UseCount = 42,
                LastUsed = new DateOnly(2025, 5, 15)
            },
            TagSuggestions = ["strategy", "strategic", "strategies", "strategy-framework"],
            SearchText = string.Empty,
            SortLabel = "Relevance",
            Toolbar = new LibraryToolbarState(),
            ActiveEditorTab = EditorTab.Edit,
            Pagination = new PaginationViewModel { PageNumber = 1, PageSize = 20, TotalCount = 100248 },
            Validation = new ValidationSummaryViewModel { State = ValidationState.Passed, Message = "Validation passed", VariableCount = 2 },
            CanSave = false,
            CanCancel = false
        };
    }
}