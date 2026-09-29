using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace RayFire
{
    /// <summary>
    /// Rayfire man physical material class.
    /// </summary>
    [Serializable]
    public class RFMaterial
    {
        private                                           string         name;
        [FormerlySerializedAs ("destructible")] public    bool           dest;
        [FormerlySerializedAs ("solidity")]     public    int            sol;
        [FormerlySerializedAs ("density")]      public    float          dens;
        public                                            float          drag;
        [FormerlySerializedAs ("angularDrag")]     public float          ang;
        [FormerlySerializedAs ("material")]        public PhysicsMaterial mat;
        [FormerlySerializedAs ("dynamicFriction")] public float          dyn;
        [FormerlySerializedAs ("staticFriction")]  public float          stat;
        [FormerlySerializedAs ("bounciness")]      public float          bnc;
        
        public RFMaterial(string Name, float Density, float Drag, float AngularDrag, 
            int Solidity, bool Dest, float DynFriction, float StFriction, float Bounce)
        {
            name = Name;
            dens = Density;
            drag = Drag;
            ang  = AngularDrag;
            sol  = Solidity;
            dest = Dest;
            dyn  = DynFriction;
            stat = StFriction;
            bnc  = Bounce;
        }

        // Get Physic material
        public PhysicsMaterial Material
        {
            get
            {
                PhysicsMaterial physMat = new PhysicsMaterial();
                physMat.name            = name;
                physMat.dynamicFriction = dyn;
                physMat.staticFriction  = stat;
                physMat.bounciness      = bnc;
                physMat.frictionCombine = PhysicsMaterialCombine.Minimum;
                return physMat;
            }
        }
    }

    /// <summary>
    /// Rayfire man physical material preset class.
    /// </summary>
    [Serializable]
    public class RFMaterialPresets
    {
        // UI properties. Do not change for material change
        public MaterialType   type;
        
        // Actual materials to change properties via code.
        public RFMaterial   heavyMetal;
        public RFMaterial   lightMetal;
        public RFMaterial   denseRock;
        public RFMaterial   porousRock;
        public RFMaterial   concrete;
        public RFMaterial   brick;
        public RFMaterial   glass;
        public RFMaterial   rubber;
        public RFMaterial   ice;
        public RFMaterial   wood;
        
        public RFMaterialPresets()
        {
            heavyMetal = new RFMaterial ("HeavyMetal", 11f,  0f, 0.05f, 80, false, 0.75f, 0.7f,  0.17f);
            lightMetal = new RFMaterial ("LightMetal", 8f,   0f, 0.05f, 50, false, 0.71f, 0.72f, 0.14f);
            denseRock  = new RFMaterial ("DenseRock",  4f,   0f, 0.05f, 22, true,  0.88f, 0.87f, 0.14f);
            porousRock = new RFMaterial ("PorousRock", 2.5f, 0f, 0.05f, 12, true,  0.84f, 0.82f, 0.16f);
            concrete   = new RFMaterial ("Concrete",   3f,   0f, 0.05f, 18, true,  0.81f, 0.83f, 0.15f);
            brick      = new RFMaterial ("Brick",      2.3f, 0f, 0.05f, 10, true,  0.76f, 0.75f, 0.13f);
            glass      = new RFMaterial ("Glass",      1.8f, 0f, 0.05f, 3,  true,  0.53f, 0.53f, 0.2f);
            rubber     = new RFMaterial ("Rubber",     1.4f, 0f, 0.05f, 1,  false, 0.95f, 0.98f, 0.93f);
            ice        = new RFMaterial ("Ice",        1f,   0f, 0.05f, 2,  true,  0.07f, 0.07f, 0f);
            wood       = new RFMaterial ("Wood",       0.7f, 0f, 0.05f, 4,  true,  0.75f, 0.73f, 0.22f);
        }

        // Create physic material if it was not applied by user
        public void SetMaterials()
        {
            if (heavyMetal.mat == null) heavyMetal.mat = heavyMetal.Material;
            if (lightMetal.mat == null) lightMetal.mat = lightMetal.Material;
            if (denseRock.mat == null) denseRock.mat   = denseRock.Material;
            if (porousRock.mat == null) porousRock.mat = porousRock.Material;
            if (concrete.mat == null) concrete.mat     = concrete.Material;
            if (brick.mat == null) brick.mat           = brick.Material;
            if (glass.mat == null) glass.mat           = glass.Material;
            if (rubber.mat == null) rubber.mat         = rubber.Material;
            if (ice.mat == null) ice.mat               = ice.Material;
            if (wood.mat == null) wood.mat             = wood.Material;
        }

        // Get density by material Type
        public float Density (MaterialType materialType)
        {
            switch (materialType)
            { 
                case MaterialType.Concrete: return concrete.dens; 
                case MaterialType.Brick: return brick.dens;
                case MaterialType.Glass: return glass.dens;
                case MaterialType.Rubber: return rubber.dens;
                case MaterialType.Ice: return ice.dens;
                case MaterialType.Wood: return wood.dens;
                case MaterialType.HeavyMetal: return heavyMetal.dens;
                case MaterialType.LightMetal: return lightMetal.dens;
                case MaterialType.DenseRock: return denseRock.dens;
                case MaterialType.PorousRock: return porousRock.dens;
            }
            return 2f;
        }
        
        // Get Drag by material Type
        public float Drag (MaterialType materialType)
        {
            switch (materialType)
            { 
                case MaterialType.Concrete: return concrete.drag; 
                case MaterialType.Brick: return brick.drag;
                case MaterialType.Glass: return glass.drag;
                case MaterialType.Rubber: return rubber.drag;
                case MaterialType.Ice: return ice.drag;
                case MaterialType.Wood: return wood.drag;
                case MaterialType.HeavyMetal: return heavyMetal.drag;
                case MaterialType.LightMetal: return lightMetal.drag;
                case MaterialType.DenseRock: return denseRock.drag;
                case MaterialType.PorousRock: return porousRock.drag;
            }
            return 0f;
        }
        
        // Get AngularDrag by material Type
        public float AngularDrag (MaterialType materialType)
        {
            switch (materialType)
            { 
                case MaterialType.Concrete: return concrete.ang; 
                case MaterialType.Brick: return brick.ang;
                case MaterialType.Glass: return glass.ang;
                case MaterialType.Rubber: return rubber.ang;
                case MaterialType.Ice: return ice.ang;
                case MaterialType.Wood: return wood.ang;
                case MaterialType.HeavyMetal: return heavyMetal.ang;
                case MaterialType.LightMetal: return lightMetal.ang;
                case MaterialType.DenseRock: return denseRock.ang;
                case MaterialType.PorousRock: return porousRock.ang;
            }
            return 0.05f;
        }

        // Get solidity by material type
        public int Solidity (MaterialType materialType)
        {
            switch (materialType)
            { 
                case MaterialType.Concrete: return concrete.sol; 
                case MaterialType.Brick: return brick.sol;
                case MaterialType.Glass: return glass.sol;
                case MaterialType.Rubber: return rubber.sol;
                case MaterialType.Ice: return ice.sol;
                case MaterialType.Wood: return wood.sol;
                case MaterialType.HeavyMetal: return heavyMetal.sol;
                case MaterialType.LightMetal: return lightMetal.sol;
                case MaterialType.DenseRock: return denseRock.sol;
                case MaterialType.PorousRock: return porousRock.sol;
            }
            return 1;
        }
        
        // Get destructible by material type
        public bool Destructible (MaterialType materialType)
        {
            switch (materialType)
            { 
                case MaterialType.Concrete: return concrete.dest; 
                case MaterialType.Brick: return brick.dest;
                case MaterialType.Glass: return glass.dest;
                case MaterialType.Rubber: return rubber.dest;
                case MaterialType.Ice: return ice.dest;
                case MaterialType.Wood: return wood.dest;
                case MaterialType.HeavyMetal: return heavyMetal.dest;
                case MaterialType.LightMetal: return lightMetal.dest;
                case MaterialType.DenseRock: return denseRock.dest;
                case MaterialType.PorousRock: return porousRock.dest;
            }
            return true;
        }
        
        // Get DynamicFriction by material Type
        public float DynamicFriction (MaterialType materialType)
        {
            switch (materialType)
            { 
                case MaterialType.Concrete: return concrete.dyn; 
                case MaterialType.Brick: return brick.dyn;
                case MaterialType.Glass: return glass.dyn;
                case MaterialType.Rubber: return rubber.dyn;
                case MaterialType.Ice: return ice.dyn;
                case MaterialType.Wood: return wood.dyn;
                case MaterialType.HeavyMetal: return heavyMetal.dyn;
                case MaterialType.LightMetal: return lightMetal.dyn;
                case MaterialType.DenseRock: return denseRock.dyn;
                case MaterialType.PorousRock: return porousRock.dyn;
            }
            return 0.5f;
        }
        
        // Get DynamicFriction by material Type
        public float StaticFriction (MaterialType materialType)
        {            
            switch (materialType)
            { 
                case MaterialType.Concrete: return concrete.stat; 
                case MaterialType.Brick: return brick.stat;
                case MaterialType.Glass: return glass.stat;
                case MaterialType.Rubber: return rubber.stat;
                case MaterialType.Ice: return ice.stat;
                case MaterialType.Wood: return wood.stat;
                case MaterialType.HeavyMetal: return heavyMetal.stat;
                case MaterialType.LightMetal: return lightMetal.stat;
                case MaterialType.DenseRock: return denseRock.stat;
                case MaterialType.PorousRock: return porousRock.stat;
            }
            return 0.5f;
        }
        
        // Get Bounciness by material Type
        public float Bounciness (MaterialType materialType)
        {
            switch (materialType)
            { 
                case MaterialType.Concrete: return concrete.bnc; 
                case MaterialType.Brick: return brick.bnc;
                case MaterialType.Glass: return glass.bnc;
                case MaterialType.Rubber: return rubber.bnc;
                case MaterialType.Ice: return ice.bnc;
                case MaterialType.Wood: return wood.bnc;
                case MaterialType.HeavyMetal: return heavyMetal.bnc;
                case MaterialType.LightMetal: return lightMetal.bnc;
                case MaterialType.DenseRock: return denseRock.bnc;
                case MaterialType.PorousRock: return porousRock.bnc;
            }
            return 0.5f;
        }
        
        // Create material by material type
        public static PhysicsMaterial PhysicMaterial (MaterialType materialType)
        {
            switch (materialType)
            { 
                case MaterialType.Concrete: return RayfireMan.inst.mp.concrete.mat;
                case MaterialType.Brick: return RayfireMan.inst.mp.brick.mat;
                case MaterialType.Glass: return RayfireMan.inst.mp.glass.mat;
                case MaterialType.Rubber: return RayfireMan.inst.mp.rubber.mat;
                case MaterialType.Ice: return RayfireMan.inst.mp.ice.mat;
                case MaterialType.Wood: return RayfireMan.inst.mp.wood.mat;
                case MaterialType.HeavyMetal: return RayfireMan.inst.mp.heavyMetal.mat;
                case MaterialType.LightMetal: return RayfireMan.inst.mp.lightMetal.mat;
                case MaterialType.DenseRock: return RayfireMan.inst.mp.denseRock.mat;
                case MaterialType.PorousRock: return RayfireMan.inst.mp.porousRock.mat;
            }
            return RayfireMan.inst.mp.concrete.mat;
        }
    }
}