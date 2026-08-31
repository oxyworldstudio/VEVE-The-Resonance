using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using VEVE.Catalog;
using VEVE.Gear;
using VEVE.WeaponCustomPro;

namespace VEVE.Content
{
    internal static class PayloadFieldIO
    {
        public static string R(float v) => v.ToString("R", CultureInfo.InvariantCulture);
        public static string Get(Dictionary<string, string> m, string k) { m.TryGetValue(k, out string v); return v ?? string.Empty; }
        public static int GetInt(Dictionary<string, string> m, string k) { int.TryParse(Get(m, k), NumberStyles.Integer, CultureInfo.InvariantCulture, out int r); return r; }
        public static float GetFloat(Dictionary<string, string> m, string k) { float.TryParse(Get(m, k), NumberStyles.Float, CultureInfo.InvariantCulture, out float r); return r; }
    }

    /// <summary>WeaponSpec <-> line payload (designer-tunable numeric profiles).</summary>
    public static class WeaponPayloadCodec
    {
        public static string Encode(WeaponSpec s)
        {
            var f = new List<KeyValuePair<string, string>>
            {
                Pair("id", s.id), Pair("name", s.displayName), Pair("maker", s.manufacturer),
                Pair("caliber", s.caliber), Pair("desc", s.description),
                Pair("role", ((int)s.role).ToString(CultureInfo.InvariantCulture)),
                Pair("proj", ((int)s.projectileType).ToString(CultureInfo.InvariantCulture)),
                Pair("mv", PayloadFieldIO.R(s.muzzleVelocity)), Pair("mass", PayloadFieldIO.R(s.bulletMass)),
                Pair("bc", PayloadFieldIO.R(s.ballisticCoefficient)), Pair("twist", PayloadFieldIO.R(s.twistRate)),
                Pair("barrel", PayloadFieldIO.R(s.barrelLength)), Pair("smooth", s.smoothbore ? "1" : "0"),
                Pair("mag", s.magazineCapacity.ToString(CultureInfo.InvariantCulture)),
                Pair("fire", PayloadFieldIO.R(s.fireInterval)), Pair("energy", PayloadFieldIO.R(s.muzzleEnergy)),
                Pair("pubenergy", PayloadFieldIO.R(s.publishedMuzzleEnergy)), Pair("dmg", PayloadFieldIO.R(s.damage)),
                Pair("eff", PayloadFieldIO.R(s.effectiveRange)), Pair("max", PayloadFieldIO.R(s.maximumRange)),
                Pair("weapmass", PayloadFieldIO.R(s.weaponMass)), Pair("recoil", PayloadFieldIO.R(s.recoilImpulse)),
                Pair("rv", PayloadFieldIO.R(s.recoilVertical)), Pair("rh", PayloadFieldIO.R(s.recoilHorizontal)),
                Pair("rcp", s.recoilProfile)
            };
            return PayloadCodec.Encode(f);
        }

        public static WeaponSpec Decode(string payload)
        {
            var m = PayloadCodec.Decode(payload);
            return new WeaponSpec
            {
                id = PayloadFieldIO.Get(m, "id"), displayName = PayloadFieldIO.Get(m, "name"),
                manufacturer = PayloadFieldIO.Get(m, "maker"), caliber = PayloadFieldIO.Get(m, "caliber"),
                description = PayloadFieldIO.Get(m, "desc"),
                role = (WeaponRole)PayloadFieldIO.GetInt(m, "role"),
                projectileType = (ProjectileType)PayloadFieldIO.GetInt(m, "proj"),
                muzzleVelocity = PayloadFieldIO.GetFloat(m, "mv"), bulletMass = PayloadFieldIO.GetFloat(m, "mass"),
                ballisticCoefficient = PayloadFieldIO.GetFloat(m, "bc"), twistRate = PayloadFieldIO.GetFloat(m, "twist"),
                barrelLength = PayloadFieldIO.GetFloat(m, "barrel"), smoothbore = PayloadFieldIO.GetInt(m, "smooth") == 1,
                magazineCapacity = PayloadFieldIO.GetInt(m, "mag"), fireInterval = PayloadFieldIO.GetFloat(m, "fire"),
                muzzleEnergy = PayloadFieldIO.GetFloat(m, "energy"), publishedMuzzleEnergy = PayloadFieldIO.GetFloat(m, "pubenergy"),
                damage = PayloadFieldIO.GetFloat(m, "dmg"), effectiveRange = PayloadFieldIO.GetFloat(m, "eff"),
                maximumRange = PayloadFieldIO.GetFloat(m, "max"), weaponMass = PayloadFieldIO.GetFloat(m, "weapmass"),
                recoilImpulse = PayloadFieldIO.GetFloat(m, "recoil"), recoilVertical = PayloadFieldIO.GetFloat(m, "rv"),
                recoilHorizontal = PayloadFieldIO.GetFloat(m, "rh"), recoilProfile = PayloadFieldIO.Get(m, "rcp")
            };
        }

        static KeyValuePair<string, string> Pair(string k, string v) => new KeyValuePair<string, string>(k, v ?? string.Empty);
    }

    /// <summary>ScopeProfile <-> line payload (published optics editable).</summary>
    public static class ScopePayloadCodec
    {
        public static string Encode(ScopeProfile p)
        {
            var f = new List<KeyValuePair<string, string>>
            {
                Pair("id", p.id), Pair("name", p.displayName), Pair("maker", p.manufacturer), Pair("reticle", p.reticleName),
                Pair("magmin", PayloadFieldIO.R(p.magnificationMin)), Pair("magmax", PayloadFieldIO.R(p.magnificationMax)),
                Pair("obj", PayloadFieldIO.R(p.objectiveDiameterMm)), Pair("tube", PayloadFieldIO.R(p.tubeDiameterMm)),
                Pair("eyerelief", PayloadFieldIO.R(p.eyeReliefMm)), Pair("fovmin", PayloadFieldIO.R(p.fovDegAtMinZoom)),
                Pair("fovmax", PayloadFieldIO.R(p.fovDegAtMaxZoom)), Pair("focal", PayloadFieldIO.R(p.referenceFocalLengthMm)),
                Pair("retunit", ((int)p.reticleUnit).ToString(CultureInfo.InvariantCulture)),
                Pair("retsub", PayloadFieldIO.R(p.reticleSubtension)),
                Pair("clickel", PayloadFieldIO.R(p.elevationClickMoa)), Pair("clickwd", PayloadFieldIO.R(p.windageClickMoa)),
                Pair("travelel", PayloadFieldIO.R(p.elevationTravelMoa)),
                Pair("fplane", ((int)p.focalPlane).ToString(CultureInfo.InvariantCulture)),
                Pair("illum", p.illuminatedReticle ? "1" : "0"),
                Pair("parmin", PayloadFieldIO.R(p.parallaxCorrectionMinRangeM)),
                Pair("parres", PayloadFieldIO.R(p.nominalResidualParallaxMoa)),
                Pair("grams", PayloadFieldIO.R(p.weightGrams)), Pair("len", PayloadFieldIO.R(p.lengthMm)),
                Pair("bore", PayloadFieldIO.R(p.boreToOpticCenterlineMm)),
                Pair("rail", ((int)p.requiredRail).ToString(CultureInfo.InvariantCulture))
            };
            return PayloadCodec.Encode(f);
        }

        public static ScopeProfile Decode(string payload)
        {
            var m = PayloadCodec.Decode(payload);
            return new ScopeProfile
            {
                id = PayloadFieldIO.Get(m, "id"), displayName = PayloadFieldIO.Get(m, "name"),
                manufacturer = PayloadFieldIO.Get(m, "maker"), reticleName = PayloadFieldIO.Get(m, "reticle"),
                magnificationMin = PayloadFieldIO.GetFloat(m, "magmin"), magnificationMax = PayloadFieldIO.GetFloat(m, "magmax"),
                objectiveDiameterMm = PayloadFieldIO.GetFloat(m, "obj"), tubeDiameterMm = PayloadFieldIO.GetFloat(m, "tube"),
                eyeReliefMm = PayloadFieldIO.GetFloat(m, "eyerelief"), fovDegAtMinZoom = PayloadFieldIO.GetFloat(m, "fovmin"),
                fovDegAtMaxZoom = PayloadFieldIO.GetFloat(m, "fovmax"), referenceFocalLengthMm = PayloadFieldIO.GetFloat(m, "focal"),
                reticleUnit = (ReticleSubtensionUnit)PayloadFieldIO.GetInt(m, "retunit"),
                reticleSubtension = PayloadFieldIO.GetFloat(m, "retsub"),
                elevationClickMoa = PayloadFieldIO.GetFloat(m, "clickel"), windageClickMoa = PayloadFieldIO.GetFloat(m, "clickwd"),
                elevationTravelMoa = PayloadFieldIO.GetFloat(m, "travelel"),
                focalPlane = (ReticleFocalPlane)PayloadFieldIO.GetInt(m, "fplane"),
                illuminatedReticle = PayloadFieldIO.GetInt(m, "illum") == 1,
                parallaxCorrectionMinRangeM = PayloadFieldIO.GetFloat(m, "parmin"),
                nominalResidualParallaxMoa = PayloadFieldIO.GetFloat(m, "parres"),
                weightGrams = PayloadFieldIO.GetFloat(m, "grams"), lengthMm = PayloadFieldIO.GetFloat(m, "len"),
                boreToOpticCenterlineMm = PayloadFieldIO.GetFloat(m, "bore"),
                requiredRail = (RailInterface)PayloadFieldIO.GetInt(m, "rail")
            };
        }

        static KeyValuePair<string, string> Pair(string k, string v) => new KeyValuePair<string, string>(k, v ?? string.Empty);
    }

    /// <summary>GearItem <-> line payload; coverage 16-zone floats as comma list (unescaped commas only as separators after escape).</summary>
    public static class GearPayloadCodec
    {
        public static string Encode(GearItem g)
        {
            var cov = new System.Text.StringBuilder();
            for (int i = 0; i < g.coveragePerZone.Length && i < GearItem.ZoneCount; i++)
            {
                if (i > 0) cov.Append(',');
                cov.Append(g.coveragePerZone[i].ToString("R", CultureInfo.InvariantCulture));
            }
            var f = new List<KeyValuePair<string, string>>
            {
                Pair("id", g.id), Pair("name", g.displayName),
                Pair("slot", ((int)g.slot).ToString(CultureInfo.InvariantCulture)),
                Pair("cat", ((int)g.category).ToString(CultureInfo.InvariantCulture)),
                Pair("prot", ((int)g.protectionLevel).ToString(CultureInfo.InvariantCulture)),
                Pair("stop", PayloadFieldIO.R(g.customStopEnergyJoules)),
                Pair("mass", PayloadFieldIO.R(g.massKg)), Pair("vol", PayloadFieldIO.R(g.volumeLitres)),
                Pair("mob", PayloadFieldIO.R(g.mobilityMultiplier)), Pair("aim", PayloadFieldIO.R(g.aimMultiplier)),
                Pair("heat", PayloadFieldIO.R(g.heatLoad)), Pair("ir", PayloadFieldIO.R(g.irSignatureMultiplier)),
                Pair("comms", g.commsIntegration ? "1" : "0"),
                Pair("cov", cov.ToString())
            };
            return PayloadCodec.Encode(f);
        }

        public static GearItem Decode(string payload)
        {
            var m = PayloadCodec.Decode(payload);
            var item = new GearItem
            {
                id = PayloadFieldIO.Get(m, "id"), displayName = PayloadFieldIO.Get(m, "name"),
                slot = (GearSlotType)PayloadFieldIO.GetInt(m, "slot"),
                category = (GearCategory)PayloadFieldIO.GetInt(m, "cat"),
                protectionLevel = (ProtectionLevel)PayloadFieldIO.GetInt(m, "prot"),
                customStopEnergyJoules = PayloadFieldIO.GetFloat(m, "stop"),
                massKg = PayloadFieldIO.GetFloat(m, "mass"), volumeLitres = PayloadFieldIO.GetFloat(m, "vol"),
                mobilityMultiplier = PayloadFieldIO.GetFloat(m, "mob"), aimMultiplier = PayloadFieldIO.GetFloat(m, "aim"),
                heatLoad = PayloadFieldIO.GetFloat(m, "heat"), irSignatureMultiplier = PayloadFieldIO.GetFloat(m, "ir"),
                commsIntegration = PayloadFieldIO.GetInt(m, "comms") == 1
            };
            string covRaw = PayloadFieldIO.Get(m, "cov");
            string[] parts = string.IsNullOrEmpty(covRaw) ? Array.Empty<string>() : covRaw.Split(',');
            for (int i = 0; i < item.coveragePerZone.Length; i++)
            {
                if (i < parts.Length && float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                    item.coveragePerZone[i] = v;
            }
            return item;
        }

        static KeyValuePair<string, string> Pair(string k, string v) => new KeyValuePair<string, string>(k, v ?? string.Empty);
    }
}
