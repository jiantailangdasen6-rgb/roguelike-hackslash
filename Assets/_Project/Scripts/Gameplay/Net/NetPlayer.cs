using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay
{
    public struct NetStatModifier : INetworkSerializable
    {
        public byte Stat;
        public byte Kind;
        public float Value;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Stat);
            serializer.SerializeValue(ref Kind);
            serializer.SerializeValue(ref Value);
        }
    }

    /// <summary>
    /// Networked player avatar. Movement is owner-authoritative (NetworkTransform in Owner mode);
    /// health and other combat state are written by the host from the simulation.
    /// </summary>
    public sealed class NetPlayer : NetworkBehaviour
    {
        public static readonly List<NetPlayer> All = new();
        public static NetPlayer Local { get; private set; }

        public readonly NetworkVariable<int> Slot = new();
        public readonly NetworkVariable<float> Hp = new();
        public readonly NetworkVariable<float> MaxHp = new();
        public readonly NetworkVariable<bool> Downed = new();
        public readonly NetworkVariable<float> ReviveProgress = new();
        public readonly NetworkVariable<float> MoveSpeed = new(4.5f);
        public readonly NetworkVariable<float> AuraRadius = new();

        [SerializeField] private SpriteRenderer body;
        [SerializeField] private SpriteRenderer aura;
        [SerializeField] private PlayerMover mover;
        [SerializeField] private PlayerInputReader input;

        private float _hurtFlash;

        public string DisplayName => $"P{Slot.Value + 1}";
        public Color Color => GameArt.PlayerColors[Mathf.Clamp(Slot.Value, 0, GameArt.PlayerColors.Length - 1)];
        public PlayerInputReader Input => input;
        public PlayerMover Mover => mover;

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            bool owner = IsOwner;
            mover.enabled = owner;
            input.enabled = owner;
            if (!owner) return;

            Local = this;
            var follow = FindAnyObjectByType<CinemachineCamera>();
            if (follow != null) follow.Follow = transform;
            SubmitEquipment();
        }

        public override void OnNetworkDespawn()
        {
            All.Remove(this);
            if (Local == this) Local = null;
        }

        /// <summary>Owner: send the equipped items' modifiers to the host.</summary>
        public void SubmitEquipment()
        {
            if (!IsOwner || !IsSpawned) return;
            var mods = StashStore.Current.GetEquippedModifiers()
                .Select(m => new NetStatModifier { Stat = (byte)m.Stat, Kind = (byte)m.Kind, Value = m.Value })
                .ToArray();
            SubmitEquipmentRpc(mods);
        }

        [Rpc(SendTo.Server)]
        private void SubmitEquipmentRpc(NetStatModifier[] mods, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            var director = RunDirector.Instance;
            if (director == null) return;
            director.ApplyEquipment(OwnerClientId,
                mods.Select(m => new StatModifier((StatType)m.Stat, (ModifierKind)m.Kind, m.Value)));
        }

        public void FlashHurt() => _hurtFlash = 0.15f;

        private void Update()
        {
            _hurtFlash -= Time.deltaTime;
            Color c = Color;
            if (Downed.Value) c = Color.Lerp(c, Color.gray, 0.7f);
            if (_hurtFlash > 0f) c = Color.Lerp(c, Color.red, 0.7f);
            body.color = c;

            float r = AuraRadius.Value;
            aura.enabled = r > 0f;
            if (r > 0f)
            {
                aura.transform.localScale = Vector3.one * r * 2f;
                aura.color = new Color(1f, 0.95f, 0.6f, 0.12f + 0.05f * Mathf.Sin(Time.time * 6f));
            }
        }
    }
}
