// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using DescriptionAttribute = System.ComponentModel.DescriptionAttribute;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.Leaderboards;
using osu.Game.Screens.Play.Leaderboards;
using osuTK;

namespace osu.Game.Screens.Select
{
    public partial class BeatmapDetailsArea
    {
        public partial class Header : CompositeDrawable
        {
            private WedgeSelector<Selection> tabControl = null!;
            private FillFlowContainer leaderboardControls = null!;

            private ShearedDropdown<BeatmapLeaderboardScope> scopeDropdown = null!;
            private ShearedDropdown<LeaderboardSortMode> sortDropdown = null!;
            private ShearedToggleButton selectedModsToggle = null!;

            public IBindable<Selection> Type => tabControl.Current;

            public IBindable<BeatmapLeaderboardScope> Scope => scopeDropdown.Current;

            private readonly Bindable<BeatmapDetailTab> configDetailTab = new Bindable<BeatmapDetailTab>();

            public IBindable<LeaderboardSortMode> Sorting => sortDropdown.Current;

            private readonly Bindable<LeaderboardSortMode> configLeaderboardSortMode = new Bindable<LeaderboardSortMode>();

            public IBindable<bool> FilterBySelectedMods => selectedModsToggle.Active;

            [BackgroundDependencyLoader]
            private void load(OsuConfigManager config)
            {
                InternalChildren = new Drawable[]
                {
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Left = SongSelect.WEDGE_CONTENT_MARGIN, Right = 5f },
                        Children = new Drawable[]
                        {
                            tabControl = new WedgeSelector<Selection>(20f)
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Width = 200,
                                Height = 22,
                                Margin = new MarginPadding { Top = 2f },
                                IsSwitchable = true,
                            },
                            // oily (ilo, 2026-11-27): osu!stable's "Global Ranking" box, shown instead of the tabs and buttons in the Stable "Menu UI".
                            stableRanking = new StableDropdown<StableRanking>
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Width = 300,
                                Alpha = 0,
                                Items = Enum.GetValues<StableRanking>(),
                                TextFor = r => r.GetDescription(),
                            },
                            leaderboardControls = new FillFlowContainer
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                RelativeSizeAxes = Axes.X,
                                Height = 30,
                                Spacing = new Vector2(5f, 0f),
                                Direction = FillDirection.Horizontal,
                                Padding = new MarginPadding { Left = 258 },
                                Children = new Drawable[]
                                {
                                    selectedModsToggle = new ShearedToggleButton
                                    {
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                        AutoSizeAxes = Axes.X,
                                        Text = UserInterfaceStrings.SelectedMods,
                                        Height = 30f,
                                        // Eyeballed to make spacing match. Because shear is silly and implemented in different ways between dropdown and button.
                                        Margin = new MarginPadding { Left = -9.2f },
                                    },
                                    sortDropdown = new ShearedDropdown<LeaderboardSortMode>(BeatmapLeaderboardWedgeStrings.Sort)
                                    {
                                        Anchor = Anchor.TopRight,
                                        Origin = Anchor.TopRight,
                                        RelativeSizeAxes = Axes.X,
                                        Width = 0.4f,
                                        Items = Enum.GetValues<LeaderboardSortMode>(),
                                    },
                                    scopeDropdown = new ScopeDropdown
                                    {
                                        Anchor = Anchor.TopRight,
                                        Origin = Anchor.TopRight,
                                        RelativeSizeAxes = Axes.X,
                                        Width = 0.4f,
                                        Current = { Value = BeatmapLeaderboardScope.Global },
                                    },
                                },
                            },
                        },
                    },
                };

                config.BindWith(OsuSetting.BeatmapDetailTab, configDetailTab);
                config.BindWith(OsuSetting.BeatmapLeaderboardSortMode, configLeaderboardSortMode);
                config.BindWith(OsuSetting.BeatmapDetailModsFilter, selectedModsToggle.Active);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                scopeDropdown.Current.Value = tryMapDetailTabToLeaderboardScope(configDetailTab.Value) ?? scopeDropdown.Current.Value;
                scopeDropdown.Current.BindValueChanged(_ => updateConfigDetailTab());

                tabControl.Current.Value = configDetailTab.Value == BeatmapDetailTab.Details ? Selection.Details : Selection.Ranking;
                tabControl.Current.BindValueChanged(v =>
                {
                    leaderboardControls.FadeTo(v.NewValue == Selection.Ranking && !stableLook.Value ? 1 : 0, 300, Easing.OutQuint);
                    updateConfigDetailTab();
                }, true);

                scopeDropdown.Current.BindValueChanged(scope =>
                {
                    sortDropdown.Current.Disabled = false;

                    if (scope.NewValue == BeatmapLeaderboardScope.Local)
                    {
                        sortDropdown.Current.BindTo(configLeaderboardSortMode);
                    }
                    else
                    {
                        // future implementation when we have web-side support.
                        sortDropdown.Current.UnbindFrom(configLeaderboardSortMode);
                        sortDropdown.Current.Value = LeaderboardSortMode.Score;
                        sortDropdown.Current.Disabled = true;
                    }
                }, true);

                loadStableLook();
            }

            protected override bool OnMouseDown(MouseDownEvent e) => true;

            #region oily: osu!stable's look

            [Resolved(CanBeNull = true)]
            private SongSelectPieceRegistry? pieceRegistry { get; set; }

            private readonly BindableBool stableLook = new BindableBool();
            private StableDropdown<StableRanking> stableRanking = null!;
            private bool updatingStableRanking;

            /// <summary>
            /// osu!stable's leaderboard choices: one list for both the scope and the selected mods filter.
            /// </summary>
            public enum StableRanking
            {
                [Description("Local Ranking")]
                Local,

                [Description("Global Ranking")]
                Global,

                [Description("Selected Mods")]
                SelectedMods,

                [Description("Friend Ranking")]
                Friends,

                [Description("Country Ranking")]
                Country,
            }

            private void loadStableLook()
            {
                if (pieceRegistry != null)
                    stableLook.BindTo(pieceRegistry.StableLeaderboard);

                scopeDropdown.Current.BindValueChanged(_ => updateStableRanking());
                selectedModsToggle.Active.BindValueChanged(_ => updateStableRanking(), true);

                stableRanking.Current.BindValueChanged(r =>
                {
                    if (updatingStableRanking)
                        return;

                    switch (r.NewValue)
                    {
                        case StableRanking.Local:
                            scopeDropdown.Current.Value = BeatmapLeaderboardScope.Local;
                            selectedModsToggle.Active.Value = false;
                            break;

                        case StableRanking.Global:
                            scopeDropdown.Current.Value = BeatmapLeaderboardScope.Global;
                            selectedModsToggle.Active.Value = false;
                            break;

                        case StableRanking.SelectedMods:
                            scopeDropdown.Current.Value = BeatmapLeaderboardScope.Global;
                            selectedModsToggle.Active.Value = true;
                            break;

                        case StableRanking.Friends:
                            scopeDropdown.Current.Value = BeatmapLeaderboardScope.Friend;
                            selectedModsToggle.Active.Value = false;
                            break;

                        case StableRanking.Country:
                            scopeDropdown.Current.Value = BeatmapLeaderboardScope.Country;
                            selectedModsToggle.Active.Value = false;
                            break;
                    }
                });

                stableLook.BindValueChanged(v =>
                {
                    // Stable only has the leaderboard here.
                    if (v.NewValue)
                        tabControl.Current.Value = Selection.Ranking;

                    tabControl.Alpha = v.NewValue ? 0 : 1;
                    stableRanking.Alpha = v.NewValue ? 1 : 0;
                    leaderboardControls.FadeTo(!v.NewValue && tabControl.Current.Value == Selection.Ranking ? 1 : 0, 300, Easing.OutQuint);
                }, true);
            }

            private void updateStableRanking()
            {
                updatingStableRanking = true;

                switch (scopeDropdown.Current.Value)
                {
                    case BeatmapLeaderboardScope.Local:
                        stableRanking.Current.Value = StableRanking.Local;
                        break;

                    case BeatmapLeaderboardScope.Friend:
                        stableRanking.Current.Value = StableRanking.Friends;
                        break;

                    case BeatmapLeaderboardScope.Country:
                        stableRanking.Current.Value = StableRanking.Country;
                        break;

                    default:
                        stableRanking.Current.Value = selectedModsToggle.Active.Value ? StableRanking.SelectedMods : StableRanking.Global;
                        break;
                }

                updatingStableRanking = false;
            }

            #endregion

            #region Reading / writing state from / to configuration

            private void updateConfigDetailTab()
            {
                switch (tabControl.Current.Value)
                {
                    case Selection.Details:
                        configDetailTab.Value = BeatmapDetailTab.Details;
                        return;

                    case Selection.Ranking:
                        configDetailTab.Value = mapLeaderboardScopeToDetailTab(scopeDropdown.Current.Value);
                        return;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(tabControl.Current.Value), tabControl.Current.Value, null);
                }
            }

            private static BeatmapLeaderboardScope? tryMapDetailTabToLeaderboardScope(BeatmapDetailTab tab)
            {
                switch (tab)
                {
                    case BeatmapDetailTab.Local:
                        return BeatmapLeaderboardScope.Local;

                    case BeatmapDetailTab.Country:
                        return BeatmapLeaderboardScope.Country;

                    case BeatmapDetailTab.Global:
                        return BeatmapLeaderboardScope.Global;

                    case BeatmapDetailTab.Friends:
                        return BeatmapLeaderboardScope.Friend;

                    case BeatmapDetailTab.Team:
                        return BeatmapLeaderboardScope.Team;

                    default:
                        return null;
                }
            }

            private static BeatmapDetailTab mapLeaderboardScopeToDetailTab(BeatmapLeaderboardScope scope)
            {
                switch (scope)
                {
                    case BeatmapLeaderboardScope.Local:
                        return BeatmapDetailTab.Local;

                    case BeatmapLeaderboardScope.Country:
                        return BeatmapDetailTab.Country;

                    case BeatmapLeaderboardScope.Global:
                        return BeatmapDetailTab.Global;

                    case BeatmapLeaderboardScope.Friend:
                        return BeatmapDetailTab.Friends;

                    case BeatmapLeaderboardScope.Team:
                        return BeatmapDetailTab.Team;

                    default:
                        throw new ArgumentOutOfRangeException(nameof(scope), scope, null);
                }
            }

            #endregion

            public enum Selection
            {
                [LocalisableDescription(typeof(SongSelectStrings), nameof(SongSelectStrings.Details))]
                Details,

                [LocalisableDescription(typeof(SongSelectStrings), nameof(SongSelectStrings.Ranking))]
                Ranking,
            }

            private partial class ScopeDropdown : ShearedDropdown<BeatmapLeaderboardScope>
            {
                public ScopeDropdown()
                    : base(BeatmapLeaderboardWedgeStrings.Scope)
                {
                    Items = Enum.GetValues<BeatmapLeaderboardScope>();
                }

                protected override LocalisableString GenerateItemText(BeatmapLeaderboardScope item) => item.GetLocalisableDescription();
            }
        }
    }
}
