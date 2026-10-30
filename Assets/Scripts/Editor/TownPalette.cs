using UnityEditor;
using UnityEngine;

namespace FullyVolted.EditorTools
{
    // Every material the town uses, shared between all buildings (few materials = few draw calls). A material that
    // already exists is reused, so a colour tweaked in the editor survives a rebuild.
    internal sealed class TownPalette
    {
        private readonly string folder;

        // walls
        public readonly Material Cream, White, Yellow, Brick, GreyBlue, Concrete, Sheet, Foundation;
        // roofs and trim
        public readonly Material RoofTerracotta, RoofSlate, RoofBrown, RoofFlat, TrimWhite, TrimDark;
        // doors, glass
        public readonly Material DoorRed, DoorGreen, DoorBlue, Glass;
        // ground
        public readonly Material Asphalt, Sidewalk, Paint, Gravel, Path;
        // yard
        public readonly Material Hedge, FenceWood, CarRed, CarBlue, CarWhite, CarGrey, Tyre, Lamp;
        // utility
        public readonly Material MetalDark, MetalLight, Safety, Ceramic, Transformer;

        public TownPalette(string folder)
        {
            this.folder = folder;

            Cream = Make("Wall Cream", new Color(0.86f, 0.82f, 0.70f));
            White = Make("Wall White", new Color(0.88f, 0.88f, 0.84f));
            Yellow = Make("Wall Yellow", new Color(0.84f, 0.72f, 0.42f));
            Brick = Make("Wall Brick", new Color(0.60f, 0.28f, 0.20f));
            GreyBlue = Make("Wall Grey Blue", new Color(0.55f, 0.62f, 0.68f));
            Concrete = Make("Concrete", new Color(0.62f, 0.62f, 0.60f));
            Sheet = Make("Metal Sheet", new Color(0.50f, 0.58f, 0.62f), 0.35f, 0.4f);
            Foundation = Make("Foundation Stone", new Color(0.42f, 0.41f, 0.40f));

            RoofTerracotta = Make("Roof Terracotta", new Color(0.62f, 0.27f, 0.18f));
            RoofSlate = Make("Roof Slate", new Color(0.22f, 0.24f, 0.28f));
            RoofBrown = Make("Roof Brown", new Color(0.35f, 0.24f, 0.18f));
            RoofFlat = Make("Roof Flat", new Color(0.25f, 0.25f, 0.26f));
            TrimWhite = Make("Trim White", new Color(0.90f, 0.90f, 0.88f));
            TrimDark = Make("Trim Dark Wood", new Color(0.25f, 0.17f, 0.12f));

            DoorRed = Make("Door Red", new Color(0.50f, 0.12f, 0.10f));
            DoorGreen = Make("Door Green", new Color(0.15f, 0.32f, 0.20f));
            DoorBlue = Make("Door Blue", new Color(0.15f, 0.25f, 0.45f));
            Glass = Make("Window Glass", new Color(0.20f, 0.30f, 0.40f), 0.8f, 0.1f);

            Asphalt = Make("Asphalt", new Color(0.16f, 0.16f, 0.17f));
            Sidewalk = Make("Sidewalk", new Color(0.62f, 0.62f, 0.60f));
            Paint = Make("Road Paint", new Color(0.85f, 0.85f, 0.80f));
            Gravel = Make("Gravel", new Color(0.45f, 0.42f, 0.36f));
            Path = Make("Garden Path", new Color(0.60f, 0.57f, 0.50f));

            Hedge = Make("Hedge", new Color(0.15f, 0.32f, 0.12f));
            FenceWood = Make("Fence Wood", new Color(0.75f, 0.70f, 0.60f));
            CarRed = Make("Car Red", new Color(0.62f, 0.12f, 0.10f), 0.5f, 0.3f);
            CarBlue = Make("Car Blue", new Color(0.12f, 0.24f, 0.50f), 0.5f, 0.3f);
            CarWhite = Make("Car White", new Color(0.88f, 0.88f, 0.88f), 0.5f, 0.3f);
            CarGrey = Make("Car Grey", new Color(0.40f, 0.42f, 0.45f), 0.5f, 0.3f);
            Tyre = Make("Tyre", new Color(0.07f, 0.07f, 0.08f));
            Lamp = Make("Lamp Glass", new Color(0.90f, 0.88f, 0.70f), 0.6f);

            MetalDark = Make("Metal Dark", new Color(0.20f, 0.22f, 0.25f), 0.4f, 0.6f);
            MetalLight = Make("Metal Light", new Color(0.62f, 0.64f, 0.67f), 0.4f, 0.6f);
            Safety = Make("Safety Yellow", new Color(0.90f, 0.75f, 0.10f));
            Ceramic = Make("Insulator Ceramic", new Color(0.55f, 0.35f, 0.20f), 0.5f);
            Transformer = Make("Transformer Grey Green", new Color(0.35f, 0.42f, 0.38f), 0.3f, 0.3f);
        }

        private Material Make(string materialName, Color color, float smoothness = 0.05f, float metallic = 0f)
        {
            var path = $"{folder}/{materialName}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            material.enableInstancing = true;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
