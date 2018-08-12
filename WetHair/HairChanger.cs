using System;
using System.Collections.Generic;
using GTA;
using GTA.Math;
using GTA.Native;

namespace WetHair
{
    public enum PedVariationData
    {
        PED_VARIATION_FACE = 0,
        PED_VARIATION_HEAD = 1,
        PED_VARIATION_HAIR = 2,
        PED_VARIATION_TORSO = 3,
        PED_VARIATION_LEGS = 4,
        PED_VARIATION_HANDS = 5,
        PED_VARIATION_FEET = 6,
        PED_VARIATION_EYES = 7,
        PED_VARIATION_ACCESSORIES = 8,
        PED_VARIATION_TASKS = 9,
        PED_VARIATION_TEXTURES = 10,
        PED_VARIATION_TORSO2 = 11
    };

    public class HairChanger : Script
    {
        private readonly HashSet<VariationInfo> _variationInfos = new HashSet<VariationInfo>();
        private bool _isWatered;

        public HairChanger()
        {
            Tick += OnTick;
            ReadHairInfos();
        }

        private void ReadHairInfos()
        {   
            for (var i = 0; i < 100; i++)
            {
                var key = $"HAIR_INFO_{i}";

                var pedModel = Settings.GetValue(key, "TARGET_MODEL", string.Empty);
                if (string.IsNullOrEmpty(pedModel)) // ped not even defined.
                    continue;

                var hash = Game.GenerateHash(pedModel);
                if (hash == 0) continue;

                var var = Settings.GetValue(key, "PED_VARIATION", PedVariationData.PED_VARIATION_HAIR);
                var wdi = Settings.GetValue(key, "WET_DRAW_INDEX", (uint)1);
                var wti = Settings.GetValue(key, "WET_TEX_INDEX", (uint)0);
                var ddi = Settings.GetValue(key, "DRY_DRAW_INDEX", (uint)0);
                var dti = Settings.GetValue(key, "DRY_TEX_INDEX", (uint)0);
                var dur = Settings.GetValue(key, "WETNESS_DURATION", 30.0f);
                var rain = Settings.GetValue(key, "ALLOW_RAIN_WETNESS", false);
                var minDepth = Settings.GetValue(key, "MIN_DEPTH", 0.5f);
                var depthBone = Settings.GetValue(key, "DEPTH_CHECK_BONE_ID", (int) Bone.SKEL_Head);

                var varInfo = new VariationInfo(hash, var, wdi, wti, ddi, dti, dur, rain, minDepth, depthBone);
                _variationInfos.Add(varInfo);
            }
        }

        private void OnTick(object sender, EventArgs eventArgs)
        {
            Ped playerPed = Game.Player.Character;
            int model = playerPed.Model.Hash;
            VariationInfo v = null;

            foreach (var var in _variationInfos)
            {
                if (var.ModelHash != model) continue;
                v = var;
                break;
            }
            if (v == null) return;

            bool isWeather = (World.Weather == Weather.Raining || World.Weather == Weather.ThunderStorm) && v.AllowRain;
            bool isUnderwater = playerPed.IsSwimmingUnderWater || (playerPed.IsSwimming && playerPed.IsSprinting);
            bool isHeadWet = (isWeather || isUnderwater) && !playerPed.IsWearingHelmet;

            if (isHeadWet)
            {
                if (_isWatered) return;

                var boneCoord = playerPed.GetBoneCoord((Bone) v.DepthBone);
                if (boneCoord != Vector3.Zero)
                {
                    unsafe
                    {
                        float depth = 0f;
                        Function.Call(Hash.GET_WATER_HEIGHT, boneCoord.X, boneCoord.Y, boneCoord.Z, &depth);
                        depth = Math.Max(depth - boneCoord.Z, 0f);

                        if (depth < v.MinDepth)
                        return;
                    }
                }

                Function.Call(Hash.SET_PED_COMPONENT_VARIATION, playerPed, (uint) v.Variation, v.WetDrawableIndex,
                    v.WetTextureIndex, 0);

                v.ResetTimer();
                _isWatered = true;
            }
            else
            {
                if (!_isWatered) return;
                if (v.m_WetnessTimer > 0)
                {
                    v.m_WetnessTimer -= Game.LastFrameTime;
                    return;
                }
                Function.Call(Hash.SET_PED_COMPONENT_VARIATION, playerPed, (uint)v.Variation,
                    v.DryDrawableIndex, v.DryTextureIndex, 0);
                _isWatered = false;
            }
        }
    }

    public class VariationInfo
    {
        public float m_WetnessTimer;
        private readonly float _duration;

        public VariationInfo(int modelHash, PedVariationData v, 
                            uint wdi, uint wti, uint ddi, uint dti, 
                            float dur, bool rain, float minDepth, int depthBone)
        {
            Variation = v;
            WetDrawableIndex = wdi;
            WetTextureIndex = wti;
            DryDrawableIndex = ddi;
            DryTextureIndex = dti;
            ModelHash = modelHash;
            _duration = dur;
            m_WetnessTimer = _duration;
            AllowRain = rain;
            MinDepth = minDepth;
            DepthBone = depthBone;
        }

        public PedVariationData Variation { get; set; }
        public uint WetDrawableIndex { get; set; }
        public uint WetTextureIndex { get; set; }
        public uint DryDrawableIndex { get; set; }
        public uint DryTextureIndex { get; set; }
        public int ModelHash { get; set; }
        public bool AllowRain { get; set; }
        public float MinDepth { get; set; }
        public int DepthBone { get; set; }

        public void ResetTimer()
        {
            m_WetnessTimer = _duration;
        }
    }
}
