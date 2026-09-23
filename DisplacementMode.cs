using System.ComponentModel.DataAnnotations;

namespace TurbulentDisplace
{
    public enum DisplacementMode
    {
        [Display(Name = "両方向")]
        Both = 0,

        [Display(Name = "水平")]
        Horizontal = 1,

        [Display(Name = "垂直")]
        Vertical = 2,

        [Display(Name = "放射")]
        Radial = 3,
    }
}
