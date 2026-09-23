using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace TurbulentDisplace
{
    [VideoEffect(
        "タービュレントディスプレイス",
        ["歪み"],
        ["Turbulent", "Displace", "タービュレント", "ディスプレイス", "ノイズ", "歪み"],
        IsAviUtlSupported = false)]
    public class TurbulentDisplaceEffect : VideoEffectBase
    {
        public override string Label => "タービュレントディスプレイス";

        [Display(GroupName = "タービュレントディスプレイス", Name = "量", Description = "変位量（ピクセル）")]
        [AnimationSlider("F1", "px", 0, 200)]
        public Animation Amount { get; } = new Animation(50, 0, 4096);

        [Display(GroupName = "タービュレントディスプレイス", Name = "サイズ", Description = "ノイズの大きさ。大きいほど粗い波になる")]
        [AnimationSlider("F1", "px", 1, 400)]
        public Animation Size { get; } = new Animation(64, 1, 4096);

        [Display(GroupName = "タービュレントディスプレイス", Name = "複雑度", Description = "fBMのオクターブ数")]
        [AnimationSlider("F0", "", 1, 10)]
        public Animation Complexity { get; } = new Animation(4, 1, 10);

        [Display(GroupName = "タービュレントディスプレイス", Name = "展開", Description = "ノイズ空間を連続的に移動させる")]
        [AnimationSlider("F1", "", 0, 360)]
        public Animation Evolution { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = "タービュレントディスプレイス", Name = "オフセットX", Description = "ノイズ空間の横オフセット")]
        [AnimationSlider("F1", "", -1000, 1000)]
        public Animation OffsetX { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = "タービュレントディスプレイス", Name = "オフセットY", Description = "ノイズ空間の縦オフセット")]
        [AnimationSlider("F1", "", -1000, 1000)]
        public Animation OffsetY { get; } = new Animation(0, -100000, 100000);

        [Display(GroupName = "タービュレントディスプレイス", Name = "乱数シード", Description = "ノイズパターンを切り替える")]
        [AnimationSlider("F0", "", 0, 1000)]
        public Animation RandomSeed { get; } = new Animation(0, 0, 100000);

        [Display(GroupName = "タービュレントディスプレイス", Name = "方向", Description = "変位の向き")]
        [EnumComboBox]
        public DisplacementMode DisplacementMode
        {
            get => displacementMode;
            set => Set(ref displacementMode, value);
        }
        DisplacementMode displacementMode = DisplacementMode.Both;

        [Display(GroupName = "タービュレントディスプレイス", Name = "端の固定", Description = "画像端の変位を弱める")]
        [AnimationSlider("F2", "", 0, 1)]
        public Animation Pinning { get; } = new Animation(0, 0, 1);

        [Display(GroupName = "タービュレントディスプレイス", Name = "端の幅", Description = "固定する端の幅（UV）")]
        [AnimationSlider("F3", "", 0, 0.5)]
        public Animation EdgeWidth { get; } = new Animation(0.05, 0.0001, 0.5);

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new TurbulentDisplaceProcessor(devices, this);
        }

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            yield break;
        }

        protected override IEnumerable<IAnimatable> GetAnimatables()
        {
            return [Amount, Size, Complexity, Evolution, OffsetX, OffsetY, RandomSeed, Pinning, EdgeWidth];
        }
    }
}
