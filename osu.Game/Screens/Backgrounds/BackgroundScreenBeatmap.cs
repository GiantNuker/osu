// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.Containers;
using osu.Game.Screens.Play;
using osuTK;

namespace osu.Game.Screens.Backgrounds
{
    public class BackgroundScreenBeatmap : BackgroundScreen
    {
        /// <summary>
        /// The amount of blur to apply when full gameplay blur is requested.
        /// </summary>
        public const float GAMEPLAY_BLUR_FACTOR = 25;

        /// <summary>
        /// The amount of blur to apply when full UI blur is requested.
        /// </summary>
        public const float UI_BLUR_FACTOR = 25;

        protected Background Background;

        private WorkingBeatmap beatmap;

        /// <summary>
        /// Whether or not gameplay dim/blur settings should be applied to this Background.
        /// </summary>
        public readonly Bindable<bool> EnableGameplayDim = new Bindable<bool>();

        /// <summary>
        /// Whether or not UI blur settings should be applied to this Background.
        /// </summary>
        public readonly Bindable<bool> EnableUIBlur = new Bindable<bool>();

        public readonly Bindable<bool> StoryboardReplacesBackground = new Bindable<bool>();

        /// <summary>
        /// The amount of blur to be applied in addition to the game-specified or UI-specified blur.
        /// </summary>
        public readonly Bindable<float> BlurAmount = new BindableFloat();

        internal readonly IBindable<bool> IsBreakTime = new Bindable<bool>();

        private readonly DimmableBackground dimmable;

        protected virtual DimmableBackground CreateFadeContainer() => new DimmableBackground { RelativeSizeAxes = Axes.Both };

        public BackgroundScreenBeatmap(WorkingBeatmap beatmap = null)
        {
            Beatmap = beatmap;

            InternalChild = dimmable = CreateFadeContainer();

            dimmable.EnableGameplayDim.BindTo(EnableGameplayDim);
            dimmable.EnableUIBlur.BindTo(EnableUIBlur);
            dimmable.IsBreakTime.BindTo(IsBreakTime);
            dimmable.BlurAmount.BindTo(BlurAmount);

            StoryboardReplacesBackground.BindTo(dimmable.StoryboardReplacesBackground);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var background = new BeatmapBackground(beatmap);
            LoadComponent(background);
            switchBackground(background);
        }

        private CancellationTokenSource cancellationSource;

        public WorkingBeatmap Beatmap
        {
            get => beatmap;
            set
            {
                if (beatmap == value && beatmap != null)
                    return;

                beatmap = value;

                Schedule(() =>
                {
                    if ((Background as BeatmapBackground)?.Beatmap.BeatmapInfo.BackgroundEquals(beatmap?.BeatmapInfo) ?? false)
                        return;

                    cancellationSource?.Cancel();
                    LoadComponentAsync(new BeatmapBackground(beatmap), switchBackground, (cancellationSource = new CancellationTokenSource()).Token);
                });
            }
        }

        private void switchBackground(BeatmapBackground b)
        {
            float newDepth = 0;

            if (Background != null)
            {
                newDepth = Background.Depth + 1;
                Background.FinishTransforms();
                Background.FadeOut(250);
                Background.Expire();
            }

            b.Depth = newDepth;
            dimmable.Background = Background = b;
        }

        public override bool Equals(BackgroundScreen other)
        {
            if (!(other is BackgroundScreenBeatmap otherBeatmapBackground)) return false;

            return base.Equals(other) && beatmap == otherBeatmapBackground.Beatmap;
        }

        public class DimmableBackground : UserDimContainer
        {
            /// <summary>
            /// The amount of blur to be applied to the background in addition to user-specified blur.
            /// </summary>
            /// <remarks>
            /// Used in contexts where there can potentially be both user and screen-specified blurring occuring at the same time, such as in <see cref="PlayerLoader"/>
            /// </remarks>
            public readonly Bindable<float> BlurAmount = new BindableFloat();

            /// <summary>
            /// Whether or not UI blur settings should be applied to this Background.
            /// </summary>
            public readonly Bindable<bool> EnableUIBlur = new Bindable<bool>();

            public Background Background
            {
                get => background;
                set
                {
                    background?.Expire();

                    base.Add(background = value);
                    background.BlurTo(blurTarget, 0, Easing.OutQuint);
                }
            }

            private Bindable<double> gameplayBlurLevel { get; set; }
            private Bindable<double> uiBlurLevel { get; set; }

            private Background background;

            public override void Add(Drawable drawable)
            {
                if (drawable is Background)
                    throw new InvalidOperationException($"Use {nameof(Background)} to set a background.");

                base.Add(drawable);
            }

            /// <summary>
            /// As an optimisation, we add the two blur portions to be applied rather than actually applying two separate blurs.
            /// </summary>
            private Vector2 blurTarget => EnableGameplayDim.Value
                ? new Vector2(BlurAmount.Value + (float)gameplayBlurLevel.Value * GAMEPLAY_BLUR_FACTOR)
                : EnableUIBlur.Value
                    ? new Vector2(BlurAmount.Value * (float)uiBlurLevel.Value * UI_BLUR_FACTOR)
                    : new Vector2(BlurAmount.Value);

            [BackgroundDependencyLoader]
            private void load(OsuConfigManager config)
            {
                gameplayBlurLevel = config.GetBindable<double>(OsuSetting.BlurLevel);
                uiBlurLevel = config.GetBindable<double>(OsuSetting.MenuBlurLevel);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                gameplayBlurLevel.ValueChanged += _ => UpdateVisuals();
                uiBlurLevel.ValueChanged += _ => UpdateVisuals();
                BlurAmount.ValueChanged += _ => UpdateVisuals();
            }

            protected override bool ShowDimContent => !ShowStoryboard.Value || !StoryboardReplacesBackground.Value; // The background needs to be hidden in the case of it being replaced by the storyboard

            protected override void UpdateVisuals()
            {
                base.UpdateVisuals();

                Background?.BlurTo(blurTarget, BACKGROUND_FADE_DURATION, Easing.OutQuint);
            }
        }
    }
}
