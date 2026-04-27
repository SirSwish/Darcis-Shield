// ViewModels/PrimInfoViewModel.cs
// Wraps the four PrmModel header bytes and exposes named, bindable, editable
// properties for the right-hand Prim Information panel.

using System;
using System.Collections.Generic;
using UrbanChaosEditor.Shared.ViewModels;
using UrbanChaosPrimEditor.Models;

namespace UrbanChaosPrimEditor.ViewModels
{
    public sealed class PrimInfoViewModel : BaseViewModel
    {
        // ── Static dropdown collections ───────────────────────────────────────

        public static IReadOnlyList<KeyValuePair<int, string>> CollisionTypes { get; } =
            new List<KeyValuePair<int, string>>
            {
                new(0, "Normal"),
                new(1, "None"),
                new(2, "Tree"),
                new(3, "Reduced"),
            };

        public static IReadOnlyList<KeyValuePair<int, string>> ShadowTypes { get; } =
            new List<KeyValuePair<int, string>>
            {
                new(0, "None"),
                new(1, "Box Edge"),
                new(2, "Cylinder"),
                new(3, "Four Legs"),
                new(4, "Full Box"),
            };

        // ── Live model reference ──────────────────────────────────────────────

        private PrmModel? _model;

        /// <summary>Raised whenever a property is written back to the model.</summary>
        public event EventHandler? ModelChanged;

        // ── Collision type ────────────────────────────────────────────────────

        public int CollisionType
        {
            get => _model?.CollisionType ?? 0;
            set
            {
                if (_model is null || _model.CollisionType == value) return;
                _model.CollisionType = value;
                RaisePropertyChanged();
                ModelChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        // ── Shadow type ───────────────────────────────────────────────────────

        public int ShadowType
        {
            get => _model?.ShadowType ?? 0;
            set
            {
                if (_model is null || _model.ShadowType == value) return;
                _model.ShadowType = value;
                RaisePropertyChanged();
                ModelChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        // ── Damage / reaction flags (ReactionToImpactByVehicle byte) ─────────

        public bool IsDamageable
        {
            get => ((_model?.ReactionToImpactByVehicle ?? 0) & 0x01) != 0;
            set => SetDamageFlag(0x01, value);
        }

        public bool Explodes
        {
            get => ((_model?.ReactionToImpactByVehicle ?? 0) & 0x02) != 0;
            set => SetDamageFlag(0x02, value);
        }

        public bool Crumples
        {
            get => ((_model?.ReactionToImpactByVehicle ?? 0) & 0x04) != 0;
            set => SetDamageFlag(0x04, value);
        }

        public bool NoLineOfSight
        {
            get => ((_model?.ReactionToImpactByVehicle ?? 0) & 0x08) != 0;
            set => SetDamageFlag(0x08, value);
        }

        public int RawDamageFlags => _model?.ReactionToImpactByVehicle ?? 0;

        // ── Various properties flags ──────────────────────────────────────────

        public bool IsLight
        {
            get => ((_model?.VariousProperties ?? 0) & 0x01) != 0;
            set => SetPropFlag(0x01, value);
        }

        public bool ContainsWalkableFaces
        {
            get => ((_model?.VariousProperties ?? 0) & 0x02) != 0;
            set => SetPropFlag(0x02, value);
        }

        public bool HasGlare
        {
            get => ((_model?.VariousProperties ?? 0) & 0x04) != 0;
            set => SetPropFlag(0x04, value);
        }

        public bool IsRotatingItem
        {
            get => ((_model?.VariousProperties ?? 0) & 0x08) != 0;
            set => SetPropFlag(0x08, value);
        }

        public bool IsTree
        {
            get => ((_model?.VariousProperties ?? 0) & 0x10) != 0;
            set => SetPropFlag(0x10, value);
        }

        public bool EnvMapped
        {
            get => ((_model?.VariousProperties ?? 0) & 0x20) != 0;
            set => SetPropFlag(0x20, value);
        }

        public bool JustFloor
        {
            get => ((_model?.VariousProperties ?? 0) & 0x40) != 0;
            set => SetPropFlag(0x40, value);
        }

        public bool OnFloor
        {
            get => ((_model?.VariousProperties ?? 0) & 0x80) != 0;
            set => SetPropFlag(0x80, value);
        }

        public int RawVariousProperties => _model?.VariousProperties ?? 0;

        // ── Bit helpers ───────────────────────────────────────────────────────

        private void SetDamageFlag(int mask, bool on)
        {
            if (_model is null) return;
            int current = _model.ReactionToImpactByVehicle;
            int next    = on ? (current | mask) : (current & ~mask);
            if (current == next) return;
            _model.ReactionToImpactByVehicle = next;
            RaisePropertyChanged(string.Empty);
            ModelChanged?.Invoke(this, EventArgs.Empty);
        }

        private void SetPropFlag(int mask, bool on)
        {
            if (_model is null) return;
            int current = _model.VariousProperties;
            int next    = on ? (current | mask) : (current & ~mask);
            if (current == next) return;
            _model.VariousProperties = next;
            RaisePropertyChanged(string.Empty);
            ModelChanged?.Invoke(this, EventArgs.Empty);
        }

        // ── Update / clear ────────────────────────────────────────────────────

        public void Update(PrmModel model)
        {
            _model = model;
            RaisePropertyChanged(string.Empty);
        }

        public void Clear()
        {
            _model = null;
            RaisePropertyChanged(string.Empty);
        }
    }
}
