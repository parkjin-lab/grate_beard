using System.IO;
using UnityEngine;

namespace LostBreadcrumbs.Runtime.Map
{
    public static class MapReadableArt
    {
        private const string WallResourcePath = "Map/ForestMossyStoneWall";
        private const string BreadResourcePath = "Map/GoldenGlowBreadcrumb";
        private const string FaintBreadResourcePath = "Map/FaintLickedBreadcrumb";
        private const string CorruptedBreadResourcePath = "Map/CorruptedFalseBreadcrumb";
        private const string LandmarkCacheResourcePath = "Map/LandmarkTrailCache";
        private const string HouseThresholdResourcePath = "Map/HouseThresholdDoorGlow";
        private const string SmokeResourcePath = "Map/ForestEchoSmokePuff";
        private const string EchoPulseResourcePath = "Map/ForestEchoPulseRing";
        private const string PatrolThreatResourcePath = "Map/ForestPatrolThreat";
        private const string SeekerThreatResourcePath = "Map/ForestSeekerThreat";
        private const string PlayerBodyResourcePath = "Map/ForestSiblingTraveler";
        private const string StageExitPortalResourcePath = "Map/ForestStageExitPortal";
        private const string SafeHavenResourcePath = "Map/ForestSafeHavenMossRing";
        private const string StaminaPickupResourcePath = "Map/ForestStaminaDewBerry";
        private const string ExitChoiceCacheResourcePath = "Map/ForestExitChoiceCache";
        private const string ExitChoiceCacheBeaconResourcePath = "Map/ForestExitChoiceCacheBeacon";
        private const string ExitUnlockBeaconResourcePath = "Map/ForestExitUnlockBeacon";
        private const string DecoyResourcePath = "Map/ForestDecoyEchoLure";
        private const string DecoyPulseResourcePath = "Map/ForestDecoyEchoPulse";
        private const string WallFileName = "ForestMossyStoneWall.png";
        private const string BreadFileName = "GoldenGlowBreadcrumb.png";
        private const string FaintBreadFileName = "FaintLickedBreadcrumb.png";
        private const string CorruptedBreadFileName = "CorruptedFalseBreadcrumb.png";
        private const string LandmarkCacheFileName = "LandmarkTrailCache.png";
        private const string HouseThresholdFileName = "HouseThresholdDoorGlow.png";
        private const string SmokeFileName = "ForestEchoSmokePuff.png";
        private const string EchoPulseFileName = "ForestEchoPulseRing.png";
        private const string PatrolThreatFileName = "ForestPatrolThreat.png";
        private const string SeekerThreatFileName = "ForestSeekerThreat.png";
        private const string PlayerBodyFileName = "ForestSiblingTraveler.png";
        private const string StageExitPortalFileName = "ForestStageExitPortal.png";
        private const string SafeHavenFileName = "ForestSafeHavenMossRing.png";
        private const string StaminaPickupFileName = "ForestStaminaDewBerry.png";
        private const string ExitChoiceCacheFileName = "ForestExitChoiceCache.png";
        private const string ExitChoiceCacheBeaconFileName = "ForestExitChoiceCacheBeacon.png";
        private const string ExitUnlockBeaconFileName = "ForestExitUnlockBeacon.png";
        private const string DecoyFileName = "ForestDecoyEchoLure.png";
        private const string DecoyPulseFileName = "ForestDecoyEchoPulse.png";

        private static Sprite wallSprite;
        private static Sprite breadSprite;
        private static Sprite faintBreadSprite;
        private static Sprite corruptedBreadSprite;
        private static Sprite landmarkCacheSprite;
        private static Sprite houseThresholdExitSprite;
        private static Sprite smokeSprite;
        private static Sprite echoPulseSprite;
        private static Sprite patrolThreatSprite;
        private static Sprite seekerThreatSprite;
        private static Sprite playerBodySprite;
        private static Sprite stageExitPortalSprite;
        private static Sprite safeHavenSprite;
        private static Sprite staminaPickupSprite;
        private static Sprite exitChoiceCacheSprite;
        private static Sprite exitChoiceCacheBeaconSprite;
        private static Sprite exitUnlockBeaconSprite;
        private static Sprite decoySprite;
        private static Sprite decoyPulseSprite;

        public static Sprite TryGetWallSprite()
        {
            if (wallSprite != null)
            {
                return wallSprite;
            }

            wallSprite = LoadSprite(WallResourcePath, WallFileName, "ForestMossyStoneWall");
            return wallSprite;
        }

        public static Sprite TryGetBreadcrumbSprite()
        {
            if (breadSprite != null)
            {
                return breadSprite;
            }

            breadSprite = LoadSprite(BreadResourcePath, BreadFileName, "GoldenGlowBreadcrumb");
            return breadSprite;
        }

        public static Sprite TryGetFaintBreadcrumbSprite()
        {
            if (faintBreadSprite != null)
            {
                return faintBreadSprite;
            }

            faintBreadSprite = LoadSprite(FaintBreadResourcePath, FaintBreadFileName, "FaintLickedBreadcrumb");
            return faintBreadSprite != null ? faintBreadSprite : TryGetBreadcrumbSprite();
        }

        public static Sprite TryGetCorruptedBreadcrumbSprite()
        {
            if (corruptedBreadSprite != null)
            {
                return corruptedBreadSprite;
            }

            corruptedBreadSprite = LoadSprite(CorruptedBreadResourcePath, CorruptedBreadFileName, "CorruptedFalseBreadcrumb");
            return corruptedBreadSprite != null ? corruptedBreadSprite : TryGetBreadcrumbSprite();
        }

        public static Sprite TryGetLandmarkCacheSprite()
        {
            if (landmarkCacheSprite != null)
            {
                return landmarkCacheSprite;
            }

            landmarkCacheSprite = LoadSprite(LandmarkCacheResourcePath, LandmarkCacheFileName, "LandmarkTrailCache");
            return landmarkCacheSprite != null ? landmarkCacheSprite : TryGetBreadcrumbSprite();
        }

        public static Sprite TryGetHouseThresholdExitSprite()
        {
            if (houseThresholdExitSprite != null)
            {
                return houseThresholdExitSprite;
            }

            houseThresholdExitSprite = LoadSprite(
                HouseThresholdResourcePath,
                HouseThresholdFileName,
                "HouseThresholdDoorGlow");
            return houseThresholdExitSprite;
        }

        public static Sprite TryGetSmokeSprite()
        {
            if (smokeSprite != null)
            {
                return smokeSprite;
            }

            smokeSprite = LoadSprite(SmokeResourcePath, SmokeFileName, "ForestEchoSmokePuff");
            return smokeSprite;
        }

        public static Sprite TryGetStaminaPickupSprite()
        {
            if (staminaPickupSprite != null)
            {
                return staminaPickupSprite;
            }

            // Same LoadSprite PPU 100 pattern as crumbs/smoke; SpawnStaminaPickup keeps localScale 0.4.
            staminaPickupSprite = LoadSprite(
                StaminaPickupResourcePath,
                StaminaPickupFileName,
                "ForestStaminaDewBerry");
            return staminaPickupSprite;
        }

        public static Sprite TryGetExitChoiceCacheSprite()
        {
            if (exitChoiceCacheSprite != null)
            {
                return exitChoiceCacheSprite;
            }

            // Same LoadSprite PPU 100 pattern as crumbs/smoke/stamina; SpawnExitChoiceCache keeps scale 0.82.
            exitChoiceCacheSprite = LoadSprite(
                ExitChoiceCacheResourcePath,
                ExitChoiceCacheFileName,
                "ForestExitChoiceCache");
            return exitChoiceCacheSprite;
        }

        public static Sprite TryGetDecoySprite()
        {
            if (decoySprite != null)
            {
                return decoySprite;
            }

            // Same LoadSprite PPU 100 pattern as crumbs/smoke/stamina; PlayerDecoyAbility keeps localScale 0.4.
            decoySprite = LoadSprite(
                DecoyResourcePath,
                DecoyFileName,
                "ForestDecoyEchoLure");
            return decoySprite;
        }

        public static Sprite TryGetDecoyPulseSprite()
        {
            if (decoyPulseSprite != null)
            {
                return decoyPulseSprite;
            }

            // Same LoadSprite PPU 100 pattern as smoke/echo/unlock beacon;
            // EchoPulseVisualDummy recreates unit-world PPU when this sprite is passed as override.
            decoyPulseSprite = LoadSprite(
                DecoyPulseResourcePath,
                DecoyPulseFileName,
                "ForestDecoyEchoPulse");
            return decoyPulseSprite;
        }

        public static Sprite TryGetExitChoiceCacheBeaconSprite()
        {
            if (exitChoiceCacheBeaconSprite != null)
            {
                return exitChoiceCacheBeaconSprite;
            }

            // Same LoadSprite PPU 100 pattern as smoke/echo/stamina/unlock beacon;
            // EchoPulseVisualDummy recreates unit-world PPU when this sprite is passed as override.
            exitChoiceCacheBeaconSprite = LoadSprite(
                ExitChoiceCacheBeaconResourcePath,
                ExitChoiceCacheBeaconFileName,
                "ForestExitChoiceCacheBeacon");
            return exitChoiceCacheBeaconSprite;
        }

        public static Sprite TryGetEchoPulseSprite()
        {
            if (echoPulseSprite != null)
            {
                return echoPulseSprite;
            }

            echoPulseSprite = LoadSprite(EchoPulseResourcePath, EchoPulseFileName, "ForestEchoPulseRing");
            return echoPulseSprite;
        }

        public static Sprite TryGetExitUnlockBeaconSprite()
        {
            if (exitUnlockBeaconSprite != null)
            {
                return exitUnlockBeaconSprite;
            }

            // Same LoadSprite PPU 100 pattern as smoke/echo/stamina; EchoPulseVisualDummy
            // recreates unit-world PPU for ring diameter sizing when this sprite is passed in.
            exitUnlockBeaconSprite = LoadSprite(
                ExitUnlockBeaconResourcePath,
                ExitUnlockBeaconFileName,
                "ForestExitUnlockBeacon");
            return exitUnlockBeaconSprite;
        }

        public static Sprite TryGetPatrolThreatSprite()
        {
            if (patrolThreatSprite != null)
            {
                return patrolThreatSprite;
            }

            // Unit-world sprite (PPU == width) so spawn scale ~0.9 matches collider radius 0.38.
            patrolThreatSprite = LoadUnitWorldSprite(
                PatrolThreatResourcePath,
                PatrolThreatFileName,
                "ForestPatrolThreat");
            return patrolThreatSprite;
        }

        public static Sprite TryGetSeekerThreatSprite()
        {
            if (seekerThreatSprite != null)
            {
                return seekerThreatSprite;
            }

            seekerThreatSprite = LoadUnitWorldSprite(
                SeekerThreatResourcePath,
                SeekerThreatFileName,
                "ForestSeekerThreat");
            return seekerThreatSprite;
        }

        public static Sprite TryGetPlayerBodySprite()
        {
            if (playerBodySprite != null)
            {
                return playerBodySprite;
            }

            // Unit-world sprite so PlayerBodyArtScale ~0.85 matches collider radius 0.35.
            playerBodySprite = LoadUnitWorldSprite(
                PlayerBodyResourcePath,
                PlayerBodyFileName,
                "ForestSiblingTraveler");
            return playerBodySprite;
        }

        public static Sprite TryGetStageExitPortalSprite()
        {
            if (stageExitPortalSprite != null)
            {
                return stageExitPortalSprite;
            }

            // Unit-world sprite; SpawnExit keeps existing exitScale (collider radius 0.55 unchanged).
            stageExitPortalSprite = LoadUnitWorldSprite(
                StageExitPortalResourcePath,
                StageExitPortalFileName,
                "ForestStageExitPortal");
            return stageExitPortalSprite;
        }

        public static Sprite TryGetSafeHavenSprite()
        {
            if (safeHavenSprite != null)
            {
                return safeHavenSprite;
            }

            // Unit-world sprite; SpawnSafeHaven keeps localScale 0.95 and trigger radius unchanged.
            safeHavenSprite = LoadUnitWorldSprite(
                SafeHavenResourcePath,
                SafeHavenFileName,
                "ForestSafeHavenMossRing");
            return safeHavenSprite;
        }

        private static Sprite LoadUnitWorldSprite(string resourcePath, string fileName, string spriteName)
        {
            Sprite loaded = LoadSprite(resourcePath, fileName, spriteName);
            if (loaded == null)
            {
                return null;
            }

            float unitPixels = Mathf.Max(1f, loaded.rect.width);
            if (Mathf.Approximately(loaded.pixelsPerUnit, unitPixels))
            {
                return loaded;
            }

            Sprite unitSprite = Sprite.Create(
                loaded.texture,
                loaded.rect,
                new Vector2(0.5f, 0.5f),
                unitPixels);
            unitSprite.name = spriteName;
            unitSprite.hideFlags = HideFlags.HideAndDontSave;
            return unitSprite;
        }

        private static Sprite LoadSprite(string resourcePath, string fileName, string spriteName)
        {
            Sprite resourceSprite = Resources.Load<Sprite>(resourcePath);
            if (resourceSprite != null)
            {
                return resourceSprite;
            }

            Texture2D texture = LoadTextureFromProjectFile(fileName);
            if (texture == null)
            {
                texture = Resources.Load<Texture2D>(resourcePath);
            }

            if (texture == null)
            {
                return null;
            }

            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            sprite.name = spriteName;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static Texture2D LoadTextureFromProjectFile(string fileName)
        {
            string[] candidates =
            {
                Path.Combine(Application.dataPath, "_Project/Resources/Map", fileName),
                Path.Combine(Application.dataPath, "_Project/Art", fileName)
            };

            for (int i = 0; i < candidates.Length; i++)
            {
                string path = candidates[i];
                if (!File.Exists(path))
                {
                    continue;
                }

                byte[] bytes = File.ReadAllBytes(path);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = Path.GetFileNameWithoutExtension(fileName),
                    hideFlags = HideFlags.HideAndDontSave
                };

                if (texture.LoadImage(bytes, markNonReadable: false))
                {
                    return texture;
                }

                Object.Destroy(texture);
            }

            return null;
        }
    }
}
