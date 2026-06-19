using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using DeckFlow.Core.Content;
using DeckFlow.Core.Integration;
using DeckFlow.Core.Knowledge;
using DeckFlow.Core.Orchestration;
using DeckFlow.Studio;
using DeckFlow.Studio.Pages;
using DeckFlow.Studio.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Xunit;

namespace DeckFlow.Studio.Tests
{
    public sealed class HarvestPageTests : BunitContext
    {
        [Fact]
        public void HarvestPage_ConfirmBlock_Success_RecordsBlockAndRefreshesBadge()
        {
            var blocked = new MapBlockedStore();
            var index = new MapSiteIndexStore();
            var (cut, maint, _, _) = RenderHarvest(
                new[] { Vid("vidA") },
                blocked,
                index);

            BrowseChannel(cut);

            cut.WaitForAssertion(() =>
            {
                Assert.Contains("T", cut.Markup);
                Assert.DoesNotContain(">Blocked<", cut.Markup);
            });

            cut.InvokeAsync(() => cut.Find("button[aria-label='Block T']").Click());

            cut.WaitForAssertion(() => Assert.Contains("Confirm Block", cut.Markup));

            blocked.Blocked.Add("vidA");

            cut.InvokeAsync(() => cut.Find("button[aria-label='Confirm block T']").Click());

            cut.WaitForAssertion(() =>
            {
                Assert.Contains("vidA", maint.BlockCalls);
                Assert.Contains(">Blocked<", cut.Markup);
            });
        }

        [Fact]
        public void HarvestPage_ConfirmBlock_ResultFailure_ShowsErrorAndLeavesBadgeUnchanged()
        {
            var blocked = new MapBlockedStore();
            var index = new MapSiteIndexStore();
            var cannedBlock = new ContentMaintenanceResult
            {
                Success = false,
                Message = "Block failed",
            };

            var (cut, maint, _, _) = RenderHarvest(
                new[] { Vid("vidF") },
                blocked,
                index,
                cannedBlock);

            BrowseChannel(cut);

            cut.InvokeAsync(() => cut.Find("button[aria-label='Block T']").Click());
            cut.WaitForAssertion(() => Assert.Contains("Confirm Block", cut.Markup));
            cut.InvokeAsync(() => cut.Find("button[aria-label='Confirm block T']").Click());

            cut.WaitForAssertion(() =>
            {
                Assert.Contains("vidF", maint.BlockCalls);
                Assert.Contains("Block failed", cut.Markup);
                Assert.Contains("Block Video", cut.Markup);
                Assert.DoesNotContain("Confirm Block", cut.Markup);
                Assert.DoesNotContain(">Blocked<", cut.Markup);
            });
        }

        [Fact]
        public void HarvestPage_ChannelBrowse_BlockedVideoRendersBlockedBadge()
        {
            var blocked = new MapBlockedStore();
            blocked.Blocked.Add("vidBlk");
            var index = new MapSiteIndexStore();

            var (cut, _, _, _) = RenderHarvest(
                new[] { Vid("vidBlk") },
                blocked,
                index);

            BrowseChannel(cut);

            cut.WaitForAssertion(() =>
            {
                Assert.Contains(">Blocked<", cut.Markup);
                Assert.True(cut.Find("button[aria-label='Block T']").HasAttribute("disabled"));
            });
        }

        [Fact]
        public void HarvestPage_AddToQueue_ZeroResolved_ShowsWarning()
        {
            var blocked = new MapBlockedStore();
            var index = new MapSiteIndexStore();
            var (cut, _, _, _) = RenderHarvest(
                Array.Empty<YouTubeChannelVideo>(),
                blocked,
                index,
                byIds: Array.Empty<YouTubeChannelVideo>());

            cut.Find("#pasteQueue").Change("notavideo");
            cut.InvokeAsync(() => cut.FindAll("button").First(b => b.TextContent.Contains("Add to Queue", StringComparison.Ordinal)).Click());

            cut.WaitForAssertion(() =>
            {
                Assert.Contains("No videos found for the pasted input", cut.Markup);
            });
        }

        [Fact]
        public void HarvestPage_BadgeArms_ApprovedAndPublished_RenderText()
        {
            var blocked = new MapBlockedStore();
            var index = new MapSiteIndexStore();
            index.Rows["vidApp"] = MakeIndexRow("vidApp", "approved", null, false);
            index.Rows["vidPub"] = MakeIndexRow("vidPub", "approved", DateTimeOffset.UtcNow, true);

            var (cut, _, _, _) = RenderHarvest(
                new[] { Vid("vidApp"), Vid("vidPub") },
                blocked,
                index);

            BrowseChannel(cut);

            cut.WaitForAssertion(() =>
            {
                Assert.Contains("Approved", cut.Markup);
                Assert.Contains("Published", cut.Markup);
            });
        }

        [Fact]
        public void HarvestPage_MultiSelectHarvest_HarvestsOnlySelectedVideos()
        {
            var blocked = new MapBlockedStore();
            var index = new MapSiteIndexStore();
            var (cut, _, harv, _) = RenderHarvest(
                new[]
                {
                    Vid("v1", "Vid 1"),
                    Vid("v2", "Vid 2"),
                    Vid("v3", "Vid 3"),
                },
                blocked,
                index);

            BrowseChannel(cut);

            cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("tbody tr").Count));

            cut.Find("input[aria-label='Select Vid 1']").Change(true);
            cut.Find("input[aria-label='Select Vid 2']").Change(true);

            cut.WaitForAssertion(() =>
            {
                var button = cut.FindAll("button").First(b => b.TextContent.Contains("Harvest Selected", StringComparison.Ordinal));
                Assert.False(button.HasAttribute("disabled"));
            });

            cut.InvokeAsync(() => cut.FindAll("button").First(b => b.TextContent.Contains("Harvest Selected", StringComparison.Ordinal)).Click());

            cut.WaitForAssertion(() =>
            {
                var allIds = harv.HarvestCalls.SelectMany(c => c ?? Array.Empty<string>()).ToList();
                Assert.Contains("v1", allIds);
                Assert.Contains("v2", allIds);
                Assert.DoesNotContain("v3", allIds);
                Assert.Equal(2, allIds.Count);
            });
        }

        private (IRenderedComponent<Harvest> Cut, FakeContentKbOrchestrator Maint, RecordingHarvestOrchestrator Harv, StubLister Lister) RenderHarvest(
            IReadOnlyList<YouTubeChannelVideo> recent,
            MapBlockedStore blocked,
            MapSiteIndexStore index,
            ContentMaintenanceResult? cannedBlock = null,
            IReadOnlyList<YouTubeChannelVideo>? byIds = null)
        {
            JSInterop.Mode = JSRuntimeMode.Loose;

            var maint = new FakeContentKbOrchestrator();
            if (cannedBlock is not null)
            {
                maint.CannedMaintenanceResult = cannedBlock;
            }

            var harv = new RecordingHarvestOrchestrator();
            var lister = new StubLister
            {
                RecentResult = recent,
                ByIdsResult = byIds ?? Array.Empty<YouTubeChannelVideo>(),
            };

            Services.AddSingleton<IYouTubeChannelVideoLister>(lister);
            Services.AddSingleton<IHarvestOrchestrator>(harv);
            Services.AddSingleton<IContentSourceManager>(new StubSourceManager());
            Services.AddSingleton<VideoStatusResolver>(BuildResolver(blocked, index));
            Services.AddSingleton<IDistillOrchestrator>(new StubDistillOrchestrator());
            Services.AddSingleton(new StudioDistillConfig(true));
            Services.AddSingleton(new SessionCapOverride());
            Services.AddSingleton<ILlmSpendLedger>(new StubLedger());
            Services.AddSingleton<IContentMaintenanceOrchestrator>(maint);

            var cut = Render<Harvest>();
            return (cut, maint, harv, lister);
        }

        private VideoStatusResolver BuildResolver(MapBlockedStore blocked, MapSiteIndexStore index)
        {
            return new VideoStatusResolver(blocked, index, new EmptySourceStore(), new EmptyVideoStore());
        }

        private static YouTubeChannelVideo Vid(string id, string title = "T")
        {
            return new YouTubeChannelVideo
            {
                VideoId = id,
                Url = $"https://youtu.be/{id}",
                Title = title,
                ChannelId = "UCchan",
                ChannelTitle = "Chan",
                PublishedUtc = DateTimeOffset.UtcNow,
            };
        }

        private static ContentSiteIndexRow MakeIndexRow(
            string id,
            string approvalStatus,
            DateTimeOffset? pushedToProdUtc,
            bool isVisible)
        {
            return new ContentSiteIndexRow
            {
                Id = 1,
                Source = "test-channel",
                Title = "t",
                VideoUrl = $"https://youtu.be/{id}",
                ArtifactPath = $"content-kb/test-channel/{id}.md",
                IndexedUtc = DateTimeOffset.UtcNow,
                ArchetypeTags = Array.Empty<string>(),
                BracketTags = Array.Empty<string>(),
                CardCategoryTags = Array.Empty<string>(),
                ApprovalStatus = approvalStatus,
                PushedToProdUtc = pushedToProdUtc,
                IsVisible = isVisible,
                YoutubeVideoId = id,
            };
        }

        private static void BrowseChannel(IRenderedComponent<Harvest> cut)
        {
            cut.Find("#channelInput").Change("https://youtube.com/@chan");
            cut.InvokeAsync(() => cut.FindAll("button").First(b => b.TextContent.Contains("Browse", StringComparison.Ordinal)).Click());
            cut.WaitForAssertion(() => Assert.DoesNotContain("Fetching channel videos", cut.Markup));
        }

        private sealed class MapBlockedStore : IBlockedVideoStore
        {
            public HashSet<string> Blocked { get; } = new();

            public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task AddBlockAsync(string youtubeVideoId, string? reason, CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task<bool> RemoveBlockAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(true);
            }

            public Task<bool> IsBlockedAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Blocked.Contains(youtubeVideoId));
            }

            public Task<IReadOnlyList<BlockedVideo>> ListBlockedAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult((IReadOnlyList<BlockedVideo>)Array.Empty<BlockedVideo>());
            }
        }

        private sealed class MapSiteIndexStore : IContentSiteIndexStore
        {
            public Dictionary<string, ContentSiteIndexRow> Rows { get; } = new();

            public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task UpsertRowAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task UpsertRowPreservingVisibilityAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task UpsertContentColumnsOnlyAsync(ContentSiteIndexRow row, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<ContentSiteIndexRow?> GetByNaturalKeyAsync(
                string naturalKeyType,
                string naturalKeyValue,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(Rows.TryGetValue(naturalKeyValue, out var row) ? row : null);
            }

            public Task<IReadOnlyList<ContentSiteIndexRow>> GetPublishedRowsAsync(CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<IReadOnlyList<ContentSiteIndexRow>> GetApprovedRowsAsync(CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<IReadOnlyList<ContentSiteIndexRow>> GetAllRowsAsync(CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<ContentSiteIndexRow?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<int> SetVisibilityAsync(long id, bool visible, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<int> SetHiddenAsync(long id, bool hidden, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<int> DeleteByIdAsync(long id, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<int> DeleteAllRowsAsync(CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<int> SetEvergreenAsync(long id, bool evergreen, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<int> SetVisibilityBySourceAsync(string source, bool visible, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<int> SetHiddenBySourceAsync(string source, bool hidden, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<int> SetApprovalStatusAsync(
                string naturalKeyType,
                string naturalKeyValue,
                string status,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<int> SetApprovalStatusAsync(
                IReadOnlyList<(string Type, string Value)> keys,
                string status,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<int> StampPushedToProdAsync(
                IReadOnlyList<(string Type, string Value)> keys,
                DateTimeOffset pushedUtc,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<int> SetVisibilityAsync(
                IReadOnlyList<(string Type, string Value)> keys,
                bool visible,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class EmptySourceStore : IContentSourceStore
        {
            public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task<long> InsertSourceAsync(
                string sourceSlug,
                string displayName,
                string sourceType,
                string sourceUrl,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<ContentSource?> GetSourceAsync(long id, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<ContentSource?> GetSourceByUrlAsync(string url, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task SetEnabledAsync(long id, bool isEnabled, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<IReadOnlyList<ContentSource>> ListEnabledSourcesAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult((IReadOnlyList<ContentSource>)Array.Empty<ContentSource>());
            }
        }

        private sealed class EmptyVideoStore : IContentVideoStore
        {
            public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
            {
                return Task.CompletedTask;
            }

            public Task<long> InsertVideoAsync(
                long sourceId,
                string? youtubeVideoId,
                string? rssGuid,
                string title,
                string videoUrl,
                DateTimeOffset? publishedUtc,
                string transcriptStatus,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<ContentVideo?> GetVideoByYoutubeIdAsync(
                long sourceId,
                string youtubeVideoId,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult<ContentVideo?>(null);
            }

            public Task<IReadOnlyList<ContentVideo>> ListVideosPendingDistillAsync(long sourceId, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task UpdateTranscriptStatusAsync(long videoId, string status, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<long> InsertTranscriptAsync(long videoId, string source, string body, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<ContentTranscriptBody?> GetLatestTranscriptAsync(long videoId, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<long> InsertSummaryAsync(long videoId, string body, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<long> InsertClipAsync(
                long videoId,
                int timestampS,
                string excerpt,
                int sortOrder,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<long> InsertTagAsync(
                long videoId,
                string dimension,
                string tagValue,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task DeleteVideoAsync(long videoId, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<int> DeleteVideoByYoutubeIdAsync(string youtubeVideoId, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<int> DeleteAllVideosAsync(CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task ClearDistillOutputAsync(long videoId, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<string?> GetDistillStatusAsync(long videoId, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task SetDistillStatusAsync(long videoId, string status, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<int> CountTranscriptsByVideoAsync(long videoId, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<int> CountSummariesByVideoAsync(long videoId, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<int> CountClipsByVideoAsync(long videoId, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<int> CountTagsByVideoAsync(long videoId, CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class StubLister : IYouTubeChannelVideoLister
        {
            public IReadOnlyList<YouTubeChannelVideo> RecentResult { get; set; } = Array.Empty<YouTubeChannelVideo>();

            public IReadOnlyList<YouTubeChannelVideo> ByIdsResult { get; set; } = Array.Empty<YouTubeChannelVideo>();

            public Task<IReadOnlyList<YouTubeChannelVideo>> ListRecentAsync(string channelUrl, int limit, int skip = 0, CancellationToken ct = default)
            {
                return Task.FromResult(RecentResult);
            }

            public Task<IReadOnlyList<YouTubeChannelVideo>> GetByIdsAsync(IReadOnlyList<string> ids, CancellationToken ct = default)
            {
                return Task.FromResult(ByIdsResult);
            }

            public Task<IReadOnlyList<YouTubeChannelVideo>> ListPlaylistAsync(string playlistUrl, int limit, int skip = 0, CancellationToken ct = default)
            {
                return Task.FromResult((IReadOnlyList<YouTubeChannelVideo>)Array.Empty<YouTubeChannelVideo>());
            }
        }

        private sealed class RecordingHarvestOrchestrator : IHarvestOrchestrator
        {
            public List<IReadOnlyList<string>?> HarvestCalls { get; } = new();

            public Task<HarvestResult> HarvestAsync(
                int limit,
                IReadOnlyList<string>? videoIds = null,
                long? sourceId = null,
                IOrchestratorProgress? progress = null,
                CancellationToken cancellationToken = default)
            {
                HarvestCalls.Add(videoIds);
                return Task.FromResult(new HarvestResult
                {
                    Success = true,
                    Captions = videoIds?.Count ?? 0,
                });
            }
        }

        private sealed class StubSourceManager : IContentSourceManager
        {
            public Task<ContentSourceResult> EnsureYoutubeSourceAsync(
                string url,
                string name,
                IOrchestratorProgress? progress = null,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new ContentSourceResult
                {
                    Success = true,
                    Outcome = ContentSourceResult.ContentSourceOutcome.Added,
                    Id = 1L,
                });
            }

            public Task<ContentSourceResult> AddSourceAsync(
                string url,
                string name,
                string type,
                IOrchestratorProgress? progress = null,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<ContentSourceResult> SetSourceEnabledAsync(
                long id,
                bool enabled,
                IOrchestratorProgress? progress = null,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }
        }

        private sealed class StubLedger : ILlmSpendLedger
        {
            public Task RecordCallAsync(
                long videoId,
                int inputTokens,
                int outputTokens,
                decimal costUsd,
                string monthKey,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<decimal> GetMonthlyTotalAsync(string yearMonth, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(0m);
            }

            public Task<bool> WouldExceedCapAsync(
                decimal projectedCallCostUsd,
                string monthKey,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public decimal GetMonthlyCapUsd()
            {
                return 15m;
            }
        }

        private sealed class StubDistillOrchestrator : IDistillOrchestrator
        {
            public Task<DistillResult> DistillAsync(
                int limit,
                bool dryRun,
                bool isSubscriptionProvider,
                bool redistill = false,
                IReadOnlyList<string>? videoIds = null,
                IOrchestratorProgress? progress = null,
                CancellationToken cancellationToken = default)
            {
                throw new NotSupportedException();
            }

            public Task<IReadOnlyList<PendingDistillVideo>> ListPendingDistillAsync(CancellationToken cancellationToken = default)
            {
                return Task.FromResult((IReadOnlyList<PendingDistillVideo>)Array.Empty<PendingDistillVideo>());
            }
        }
    }
}
