// ViewModels/PrimInfoViewModel.cs
// Wraps the four PrmModel header bytes and exposes named, bindable properties
// for the right-hand Prim Information panel.

using UrbanChaosEditor.Shared.ViewModels;
using UrbanChaosPrimEditor.Models;

namespace UrbanChaosPrimEditor.ViewModels
{
    public sealed class PrimInfoViewModel : BaseViewModel
    {
        // ── Raw header values ─────────────────────────────────────────────────

        private int _collisionType;
        private int _reactionToImpact;
        private int _shadowType;
        private int _variousProperties;

        public int CollisionType        => _collisionType;
        public int ShadowType           => _shadowType;
        public int RawDamageFlags       => _reactionToImpact;
        public int RawVariousProperties => _variousProperties;

        // ── Collision type ────────────────────────────────────────────────────

        public string CollisionTypeDisplay => _collisionType switch
        {
            0 => "0 — None",
            1 => "1 — Standard",
            2 => "2 — Exact Mesh",
            3 => "3 — Cylinder",
            4 => "4 — Sphere",
            _ => $"{_collisionType} — Unknown"
        };

        // ── Shadow type ───────────────────────────────────────────────────────

        public string ShadowTypeDisplay => _shadowType switch
        {
            0 => "0 — None",
            1 => "1 — Simple",
            2 => "2 — Projected",
            3 => "3 — Full",
            _ => $"{_shadowType} — Unknown"
        };

        // ── Damage / reaction flags (ReactionToImpactByVehicle byte) ─────────

        public bool IsDamageable  => (_reactionToImpact & 0x01) != 0;
        public bool Explodes      => (_reactionToImpact & 0x02) != 0;
        public bool Crumples      => (_reactionToImpact & 0x04) != 0;
        public bool NoLineOfSight => (_reactionToImpact & 0x08) != 0;

        // ── Various properties flags ──────────────────────────────────────────

        public bool IsLight               => (_variousProperties & 0x01) != 0;
        public bool ContainsWalkableFaces => (_variousProperties & 0x02) != 0;
        public bool HasGlare              => (_variousProperties & 0x04) != 0;
        public bool IsRotatingItem        => (_variousProperties & 0x08) != 0;
        public bool IsTree                => (_variousProperties & 0x10) != 0;
        public bool EnvMapped             => (_variousProperties & 0x20) != 0;
        public bool JustFloor             => (_variousProperties & 0x40) != 0;
        public bool OnFloor               => (_variousProperties & 0x80) != 0;

        // ── Update / clear ────────────────────────────────────────────────────

        public void Update(PrmModel model)
        {
            _collisionType      = model.CollisionType;
            _reactionToImpact   = model.ReactionToImpactByVehicle;
            _shadowType         = model.ShadowType;
            _variousProperties  = model.VariousProperties;
            RaisePropertyChanged(string.Empty); // notify all bindings at once
        }

        public void Clear()
        {
            _collisionType     = 0;
            _reactionToImpact  = 0;
            _shadowType        = 0;
            _variousProperties = 0;
            RaisePropertyChanged(string.Empty);
        }
    }
}
