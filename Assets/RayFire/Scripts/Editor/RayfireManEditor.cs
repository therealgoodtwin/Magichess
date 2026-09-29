using UnityEngine;
using UnityEditor;
using RayFire;

namespace RayFireEditor
{
    [CanEditMultipleObjects]
    [CustomEditor (typeof(RayfireMan))]
    public class RayfireManEditor : Editor
    {
        RayfireMan man;
        Texture2D  logo;
        Texture2D  icon;
        
        // Foldout
        static bool fld_adv;
        static bool fld_mat;


        // Minimum & Maximum ranges
        const float grav_mlt_min       = -2;
        const float grav_mlt_max       = 2f;
        const float collider_size_min  = 0;
        const float collider_size_max  = 1f;
        const int   coplanar_verts_min = 0;
        const int   coplanar_verts_max = 999;
        const float minimum_mass_min   = 0.001f;
        const float minimum_mass_max   = 1f;
        const float maximum_mass_min   = 0.1f;
        const float maximum_mass_max   = 4000f;
        const float solidity_min       = 0f;
        const float solidity_max       = 5f;
        const float quota_min          = 0f;
        const float quota_max          = 0.1f;
        const float shadow_min         = 0;
        const float shadow_max         = 1f;
        const int   frag_cap_min       = 0;
        const int   frag_cap_max       = 10000;
        const int   mat_sol_min        = 0;
        const int   mat_sol_max        = 100;
        const float mat_dens_min       = 0.01f;
        const float mat_dens_max       = 100f;
        const float mat_drag_min       = 0f;
        const float mat_drag_max       = 1f;
        const float mat_ang_min        = 0f;
        const float mat_ang_max        = 1f;
        const float mat_dyn_min        = 0f;
        const float mat_dyn_max        = 1f;

        // Serialized properties
        SerializedProperty sp_phy_set;
        SerializedProperty sp_phy_int;
        SerializedProperty sp_phy_mul;
        SerializedProperty sp_phy_col;
        SerializedProperty sp_phy_cok;
        SerializedProperty sp_col_mesh;
        SerializedProperty sp_col_cls;
        
        SerializedProperty sp_mat_min;
        SerializedProperty sp_mat_max;
        SerializedProperty sp_mat_type;
        
        SerializedProperty sp_mat_0_dest;
        SerializedProperty sp_mat_0_sol;
        SerializedProperty sp_mat_0_dens;
        SerializedProperty sp_mat_0_drag;
        SerializedProperty sp_mat_0_ang;
        SerializedProperty sp_mat_0_mat;
        SerializedProperty sp_mat_0_dyn;
        SerializedProperty sp_mat_0_stat;
        SerializedProperty sp_mat_0_bnc;
        
        SerializedProperty sp_mat_1_dest;
        SerializedProperty sp_mat_1_sol;
        SerializedProperty sp_mat_1_dens;
        SerializedProperty sp_mat_1_drag;
        SerializedProperty sp_mat_1_ang;
        SerializedProperty sp_mat_1_mat;
        SerializedProperty sp_mat_1_dyn;
        SerializedProperty sp_mat_1_stat;
        SerializedProperty sp_mat_1_bnc;
        
        SerializedProperty sp_mat_2_dest;
        SerializedProperty sp_mat_2_sol;
        SerializedProperty sp_mat_2_dens;
        SerializedProperty sp_mat_2_drag;
        SerializedProperty sp_mat_2_ang;
        SerializedProperty sp_mat_2_mat;
        SerializedProperty sp_mat_2_dyn;
        SerializedProperty sp_mat_2_stat;
        SerializedProperty sp_mat_2_bnc;
        
        SerializedProperty sp_mat_3_dest;
        SerializedProperty sp_mat_3_sol;
        SerializedProperty sp_mat_3_dens;
        SerializedProperty sp_mat_3_drag;
        SerializedProperty sp_mat_3_ang;
        SerializedProperty sp_mat_3_mat;
        SerializedProperty sp_mat_3_dyn;
        SerializedProperty sp_mat_3_stat;
        SerializedProperty sp_mat_3_bnc;
        
        SerializedProperty sp_mat_4_dest;
        SerializedProperty sp_mat_4_sol;
        SerializedProperty sp_mat_4_dens;
        SerializedProperty sp_mat_4_drag;
        SerializedProperty sp_mat_4_ang;
        SerializedProperty sp_mat_4_mat;
        SerializedProperty sp_mat_4_dyn;
        SerializedProperty sp_mat_4_stat;
        SerializedProperty sp_mat_4_bnc;
        
        SerializedProperty sp_mat_5_dest;
        SerializedProperty sp_mat_5_sol;
        SerializedProperty sp_mat_5_dens;
        SerializedProperty sp_mat_5_drag;
        SerializedProperty sp_mat_5_ang;
        SerializedProperty sp_mat_5_mat;
        SerializedProperty sp_mat_5_dyn;
        SerializedProperty sp_mat_5_stat;
        SerializedProperty sp_mat_5_bnc;
        
        SerializedProperty sp_mat_6_dest;
        SerializedProperty sp_mat_6_sol;
        SerializedProperty sp_mat_6_dens;
        SerializedProperty sp_mat_6_drag;
        SerializedProperty sp_mat_6_ang;
        SerializedProperty sp_mat_6_mat;
        SerializedProperty sp_mat_6_dyn;
        SerializedProperty sp_mat_6_stat;
        SerializedProperty sp_mat_6_bnc;
        
        SerializedProperty sp_mat_7_dest;
        SerializedProperty sp_mat_7_sol;
        SerializedProperty sp_mat_7_dens;
        SerializedProperty sp_mat_7_drag;
        SerializedProperty sp_mat_7_ang;
        SerializedProperty sp_mat_7_mat;
        SerializedProperty sp_mat_7_dyn;
        SerializedProperty sp_mat_7_stat;
        SerializedProperty sp_mat_7_bnc;
        
        SerializedProperty sp_mat_8_dest;
        SerializedProperty sp_mat_8_sol;
        SerializedProperty sp_mat_8_dens;
        SerializedProperty sp_mat_8_drag;
        SerializedProperty sp_mat_8_ang;
        SerializedProperty sp_mat_8_mat;
        SerializedProperty sp_mat_8_dyn;
        SerializedProperty sp_mat_8_stat;
        SerializedProperty sp_mat_8_bnc;
        
        SerializedProperty sp_mat_9_dest;
        SerializedProperty sp_mat_9_sol;
        SerializedProperty sp_mat_9_dens;
        SerializedProperty sp_mat_9_drag;
        SerializedProperty sp_mat_9_ang;
        SerializedProperty sp_mat_9_mat;
        SerializedProperty sp_mat_9_dyn;
        SerializedProperty sp_mat_9_stat;
        SerializedProperty sp_mat_9_bnc;
        
        SerializedProperty sp_act_par;
        SerializedProperty sp_dml_sol;
        SerializedProperty sp_dml_time;
        SerializedProperty sp_dml_quota;
        SerializedProperty sp_adv_parent;
        SerializedProperty sp_adv_global;
        SerializedProperty sp_adv_current;
        SerializedProperty sp_adv_amount;
        SerializedProperty sp_adv_size;
        SerializedProperty sp_pol_frg;
        SerializedProperty sp_pol_prt;
        SerializedProperty sp_pol_reu;
        SerializedProperty sp_pol_min;
        SerializedProperty sp_pol_max;
        SerializedProperty sp_dbg_msg;
        SerializedProperty sp_dbg_bld;
        SerializedProperty sp_dbg_edt;
        
        private void OnEnable()
        {
            // Get component
            man = (RayfireMan)target;
            
            // Find properties
            sp_phy_set  = serializedObject.FindProperty(nameof(man.setGravity));
            sp_phy_mul  = serializedObject.FindProperty(nameof(man.multiplier));
            sp_phy_int  = serializedObject.FindProperty(nameof(man.interpolation));
            sp_phy_col  = serializedObject.FindProperty(nameof(man.colliderSize));
            sp_phy_cok  = serializedObject.FindProperty(nameof(man.cookingOptions));
            sp_col_mesh = serializedObject.FindProperty(nameof(man.meshCollision));
            sp_col_cls  = serializedObject.FindProperty(nameof(man.clusterCollision));
            
            sp_mat_min  = serializedObject.FindProperty(nameof(man.minimumMass));
            sp_mat_max  = serializedObject.FindProperty(nameof(man.maximumMass));
            sp_mat_type = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.type));
            
            sp_mat_0_dest = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.heavyMetal) + "." + nameof(man.mp.heavyMetal.dest));
            sp_mat_0_sol  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.heavyMetal) + "." + nameof(man.mp.heavyMetal.sol));
            sp_mat_0_dens = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.heavyMetal) + "." + nameof(man.mp.heavyMetal.dens));
            sp_mat_0_drag = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.heavyMetal) + "." + nameof(man.mp.heavyMetal.drag));
            sp_mat_0_ang  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.heavyMetal) + "." + nameof(man.mp.heavyMetal.ang));
            sp_mat_0_mat  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.heavyMetal) + "." + nameof(man.mp.heavyMetal.mat));
            sp_mat_0_dyn  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.heavyMetal) + "." + nameof(man.mp.heavyMetal.dyn));
            sp_mat_0_stat = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.heavyMetal) + "." + nameof(man.mp.heavyMetal.stat));
            sp_mat_0_bnc  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.heavyMetal) + "." + nameof(man.mp.heavyMetal.bnc));
            
            sp_mat_1_dest = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.lightMetal) + "." + nameof(man.mp.lightMetal.dest));
            sp_mat_1_sol  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.lightMetal) + "." + nameof(man.mp.lightMetal.sol));
            sp_mat_1_dens = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.lightMetal) + "." + nameof(man.mp.lightMetal.dens));
            sp_mat_1_drag = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.lightMetal) + "." + nameof(man.mp.lightMetal.drag));
            sp_mat_1_ang  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.lightMetal) + "." + nameof(man.mp.lightMetal.ang));
            sp_mat_1_mat  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.lightMetal) + "." + nameof(man.mp.lightMetal.mat));
            sp_mat_1_dyn  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.lightMetal) + "." + nameof(man.mp.lightMetal.dyn));
            sp_mat_1_stat = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.lightMetal) + "." + nameof(man.mp.lightMetal.stat));
            sp_mat_1_bnc  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.lightMetal) + "." + nameof(man.mp.lightMetal.bnc));
            
            sp_mat_2_dest = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.denseRock) + "." + nameof(man.mp.denseRock.dest));
            sp_mat_2_sol  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.denseRock) + "." + nameof(man.mp.denseRock.sol));
            sp_mat_2_dens = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.denseRock) + "." + nameof(man.mp.denseRock.dens));
            sp_mat_2_drag = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.denseRock) + "." + nameof(man.mp.denseRock.drag));
            sp_mat_2_ang  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.denseRock) + "." + nameof(man.mp.denseRock.ang));
            sp_mat_2_mat  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.denseRock) + "." + nameof(man.mp.denseRock.mat));
            sp_mat_2_dyn  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.denseRock) + "." + nameof(man.mp.denseRock.dyn));
            sp_mat_2_stat = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.denseRock) + "." + nameof(man.mp.denseRock.stat));
            sp_mat_2_bnc  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.denseRock) + "." + nameof(man.mp.denseRock.bnc));
            
            sp_mat_3_dest = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.porousRock) + "." + nameof(man.mp.porousRock.dest));
            sp_mat_3_sol  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.porousRock) + "." + nameof(man.mp.porousRock.sol));
            sp_mat_3_dens = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.porousRock) + "." + nameof(man.mp.porousRock.dens));
            sp_mat_3_drag = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.porousRock) + "." + nameof(man.mp.porousRock.drag));
            sp_mat_3_ang  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.porousRock) + "." + nameof(man.mp.porousRock.ang));
            sp_mat_3_mat  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.porousRock) + "." + nameof(man.mp.porousRock.mat));
            sp_mat_3_dyn  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.porousRock) + "." + nameof(man.mp.porousRock.dyn));
            sp_mat_3_stat = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.porousRock) + "." + nameof(man.mp.porousRock.stat));
            sp_mat_3_bnc  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.porousRock) + "." + nameof(man.mp.porousRock.bnc));
            
            sp_mat_4_dest = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.concrete) + "." + nameof(man.mp.concrete.dest));
            sp_mat_4_sol  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.concrete) + "." + nameof(man.mp.concrete.sol));
            sp_mat_4_dens = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.concrete) + "." + nameof(man.mp.concrete.dens));
            sp_mat_4_drag = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.concrete) + "." + nameof(man.mp.concrete.drag));
            sp_mat_4_ang  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.concrete) + "." + nameof(man.mp.concrete.ang));
            sp_mat_4_mat  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.concrete) + "." + nameof(man.mp.concrete.mat));
            sp_mat_4_dyn  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.concrete) + "." + nameof(man.mp.concrete.dyn));
            sp_mat_4_stat = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.concrete) + "." + nameof(man.mp.concrete.stat));
            sp_mat_4_bnc  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.concrete) + "." + nameof(man.mp.concrete.bnc));
            
            sp_mat_5_dest = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.brick) + "." + nameof(man.mp.brick.dest));
            sp_mat_5_sol  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.brick) + "." + nameof(man.mp.brick.sol));
            sp_mat_5_dens = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.brick) + "." + nameof(man.mp.brick.dens));
            sp_mat_5_drag = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.brick) + "." + nameof(man.mp.brick.drag));
            sp_mat_5_ang  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.brick) + "." + nameof(man.mp.brick.ang));
            sp_mat_5_mat  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.brick) + "." + nameof(man.mp.brick.mat));
            sp_mat_5_dyn  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.brick) + "." + nameof(man.mp.brick.dyn));
            sp_mat_5_stat = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.brick) + "." + nameof(man.mp.brick.stat));
            sp_mat_5_bnc  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.brick) + "." + nameof(man.mp.brick.bnc));
            
            sp_mat_6_dest = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.glass) + "." + nameof(man.mp.glass.dest));
            sp_mat_6_sol  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.glass) + "." + nameof(man.mp.glass.sol));
            sp_mat_6_dens = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.glass) + "." + nameof(man.mp.glass.dens));
            sp_mat_6_drag = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.glass) + "." + nameof(man.mp.glass.drag));
            sp_mat_6_ang  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.glass) + "." + nameof(man.mp.glass.ang));
            sp_mat_6_mat  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.glass) + "." + nameof(man.mp.glass.mat));
            sp_mat_6_dyn  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.glass) + "." + nameof(man.mp.glass.dyn));
            sp_mat_6_stat = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.glass) + "." + nameof(man.mp.glass.stat));
            sp_mat_6_bnc  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.glass) + "." + nameof(man.mp.glass.bnc));
            
            sp_mat_7_dest = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.rubber) + "." + nameof(man.mp.rubber.dest));
            sp_mat_7_sol  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.rubber) + "." + nameof(man.mp.rubber.sol));
            sp_mat_7_dens = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.rubber) + "." + nameof(man.mp.rubber.dens));
            sp_mat_7_drag = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.rubber) + "." + nameof(man.mp.rubber.drag));
            sp_mat_7_ang  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.rubber) + "." + nameof(man.mp.rubber.ang));
            sp_mat_7_mat  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.rubber) + "." + nameof(man.mp.rubber.mat));
            sp_mat_7_dyn  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.rubber) + "." + nameof(man.mp.rubber.dyn));
            sp_mat_7_stat = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.rubber) + "." + nameof(man.mp.rubber.stat));
            sp_mat_7_bnc  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.rubber) + "." + nameof(man.mp.rubber.bnc));
            
            sp_mat_8_dest = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.ice) + "." + nameof(man.mp.ice.dest));
            sp_mat_8_sol  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.ice) + "." + nameof(man.mp.ice.sol));
            sp_mat_8_dens = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.ice) + "." + nameof(man.mp.ice.dens));
            sp_mat_8_drag = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.ice) + "." + nameof(man.mp.ice.drag));
            sp_mat_8_ang  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.ice) + "." + nameof(man.mp.ice.ang));
            sp_mat_8_mat  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.ice) + "." + nameof(man.mp.ice.mat));
            sp_mat_8_dyn  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.ice) + "." + nameof(man.mp.ice.dyn));
            sp_mat_8_stat = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.ice) + "." + nameof(man.mp.ice.stat));
            sp_mat_8_bnc  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.ice) + "." + nameof(man.mp.ice.bnc));
            
            sp_mat_9_dest = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.wood) + "." + nameof(man.mp.wood.dest));
            sp_mat_9_sol  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.wood) + "." + nameof(man.mp.wood.sol));
            sp_mat_9_dens = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.wood) + "." + nameof(man.mp.wood.dens));
            sp_mat_9_drag = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.wood) + "." + nameof(man.mp.wood.drag));
            sp_mat_9_ang  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.wood) + "." + nameof(man.mp.wood.ang));
            sp_mat_9_mat  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.wood) + "." + nameof(man.mp.wood.mat));
            sp_mat_9_dyn  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.wood) + "." + nameof(man.mp.wood.dyn));
            sp_mat_9_stat = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.wood) + "." + nameof(man.mp.wood.stat));
            sp_mat_9_bnc  = serializedObject.FindProperty(nameof(man.mp) + "." + nameof(man.mp.wood) + "." + nameof(man.mp.wood.bnc));

            sp_act_par     = serializedObject.FindProperty(nameof(man.parent));
            sp_dml_sol     = serializedObject.FindProperty(nameof(man.globalSolidity));
            sp_dml_time    = serializedObject.FindProperty(nameof(man.timeQuota));
            sp_dml_quota   = serializedObject.FindProperty(nameof(man.quotaAction));
            sp_adv_parent  = serializedObject.FindProperty(nameof(man.adp) + "." + nameof(man.adp.parent));
            sp_adv_global  = serializedObject.FindProperty(nameof(man.adp) + "." + nameof(man.adp.globalParent));
            sp_adv_current = serializedObject.FindProperty(nameof(man.adp) + "." + nameof(man.adp.currentAmount));
            sp_adv_amount  = serializedObject.FindProperty(nameof(man.adp) + "." + nameof(man.adp.maximumAmount));
            sp_adv_size    = serializedObject.FindProperty(nameof(man.adp) + "." + nameof(man.adp.sizeThreshold));
            sp_pol_frg     = serializedObject.FindProperty(nameof(man.fragments) + "." + nameof(man.fragments.enable));
            sp_pol_reu     = serializedObject.FindProperty(nameof(man.fragments) + "." + nameof(man.fragments.reuse));
            sp_pol_min     = serializedObject.FindProperty(nameof(man.fragments) + "." + nameof(man.fragments.minCap));
            sp_pol_max     = serializedObject.FindProperty(nameof(man.fragments) + "." + nameof(man.fragments.maxCap));
            sp_pol_prt     = serializedObject.FindProperty(nameof(man.particles) + "." + nameof(man.particles.enable));
            sp_dbg_msg     = serializedObject.FindProperty(nameof(man.debugState));
            sp_dbg_bld     = serializedObject.FindProperty(nameof(man.debugBuild));
            sp_dbg_edt     = serializedObject.FindProperty(nameof(man.debugEditor));

            // Foldouts
            if (EditorPrefs.HasKey (TextKeys.man_fld_adv) == true) fld_adv = EditorPrefs.GetBool (TextKeys.man_fld_adv);
        }
        
        /// /////////////////////////////////////////////////////////
        /// Inspector
        /// /////////////////////////////////////////////////////////
        
        public override void OnInspectorGUI()
        {
            // Update changed properties
            serializedObject.Update();
            
            // Set new static instance
            if (RayfireMan.inst == null)
                RayfireMan.inst = man;
            
            if (Application.isPlaying == true)
            {
                if (GUILayout.Button (TextMan.gui_btn_dest_frags, GUILayout.Height (20)))
                    RayfireMan.inst.storage.DestroyAll();
                RFUI.Space ();
            }
            
            GUI_Physics();
            GUI_Collision();
            GUI_Materials();
            GUI_Activation();
            UI_Demolition();
            UI_Pooling();
            UI_Info();
            UI_About();
            
            // Apply changes
            serializedObject.ApplyModifiedProperties();
        }

        /// /////////////////////////////////////////////////////////
        /// Physics
        /// /////////////////////////////////////////////////////////
        
        void GUI_Physics()
        {
            RFUI.CaptionBox (TextMan.gui_cap_phy);
            
            EditorGUI.BeginChangeCheck();
            RFUI.PropertyField (sp_phy_set, TextMan.gui_phy_set);
            if (man.setGravity == true)
                RFUI.Slider (sp_phy_mul, grav_mlt_min, grav_mlt_max, TextMan.gui_phy_mul);
            RFUI.PropertyField (sp_phy_int, TextMan.gui_phy_int);
            if (EditorGUI.EndChangeCheck() == true && Application.isPlaying == true)
                man.SetGravity();
            
            RFUI.CaptionBox (TextMan.gui_cap_col);
            RFUI.Slider (sp_phy_col, collider_size_min,  collider_size_max,  TextMan.gui_phy_col);
            RFUI.PropertyField (sp_phy_cok, TextMan.gui_phy_cok);
        }

        /// /////////////////////////////////////////////////////////
        /// Collision
        /// /////////////////////////////////////////////////////////
        
        void GUI_Collision()
        {
            RFUI.CaptionBox (TextMan.gui_cap_det);
            RFUI.PropertyField (sp_col_mesh, TextMan.gui_col_mesh);
            RFUI.PropertyField (sp_col_cls,  TextMan.gui_col_cls);
        }

        /// /////////////////////////////////////////////////////////
        /// Materials
        /// /////////////////////////////////////////////////////////
        
        void GUI_Materials()
        {
            RFUI.CaptionBox (TextMan.gui_cap_mat);
            RFUI.Slider (sp_mat_min, minimum_mass_min, minimum_mass_max, TextMan.gui_mat_min);
            RFUI.Slider (sp_mat_max, maximum_mass_min, maximum_mass_max, TextMan.gui_mat_max);
            EditorGUI.BeginChangeCheck();
            fld_mat = EditorGUILayout.Foldout (fld_mat, TextMan.gui_mat_pres, true);
            if (fld_mat == true)
            {
                RFUI.Space ();
                EditorGUI.indentLevel++;
                RFUI.PropertyField (sp_mat_type, TextMan.gui_mat_type);
                RFUI.Caption (TextMan.gui_cap_dm);
                GUI_Material_Type ((MaterialType)sp_mat_type.intValue);
                EditorGUI.indentLevel--;
            }
        }
        
        void GUI_Material_Type(MaterialType type)
        {
            switch (type)
            { 
                case MaterialType.HeavyMetal: { GUI_Material_Pres(ref sp_mat_0_dest, ref sp_mat_0_sol, ref sp_mat_0_dens, ref sp_mat_0_drag, ref sp_mat_0_ang, ref sp_mat_0_mat, ref sp_mat_0_dyn, ref sp_mat_0_stat, ref sp_mat_0_bnc); break; }
                case MaterialType.LightMetal: { GUI_Material_Pres(ref sp_mat_1_dest, ref sp_mat_1_sol, ref sp_mat_1_dens, ref sp_mat_1_drag, ref sp_mat_1_ang, ref sp_mat_1_mat, ref sp_mat_1_dyn, ref sp_mat_1_stat, ref sp_mat_1_bnc); break; }
                case MaterialType.DenseRock:  { GUI_Material_Pres(ref sp_mat_2_dest, ref sp_mat_2_sol, ref sp_mat_2_dens, ref sp_mat_2_drag, ref sp_mat_2_ang, ref sp_mat_2_mat, ref sp_mat_2_dyn, ref sp_mat_2_stat, ref sp_mat_2_bnc); break; }
                case MaterialType.PorousRock: { GUI_Material_Pres(ref sp_mat_3_dest, ref sp_mat_3_sol, ref sp_mat_3_dens, ref sp_mat_3_drag, ref sp_mat_3_ang, ref sp_mat_3_mat, ref sp_mat_3_dyn, ref sp_mat_3_stat, ref sp_mat_3_bnc); break; }
                case MaterialType.Concrete:   { GUI_Material_Pres(ref sp_mat_4_dest, ref sp_mat_4_sol, ref sp_mat_4_dens, ref sp_mat_4_drag, ref sp_mat_4_ang, ref sp_mat_4_mat, ref sp_mat_4_dyn, ref sp_mat_4_stat, ref sp_mat_4_bnc); break; }
                case MaterialType.Brick:      { GUI_Material_Pres(ref sp_mat_5_dest, ref sp_mat_5_sol, ref sp_mat_5_dens, ref sp_mat_5_drag, ref sp_mat_5_ang, ref sp_mat_5_mat, ref sp_mat_5_dyn, ref sp_mat_5_stat, ref sp_mat_5_bnc); break; }
                case MaterialType.Glass:      { GUI_Material_Pres(ref sp_mat_6_dest, ref sp_mat_6_sol, ref sp_mat_6_dens, ref sp_mat_6_drag, ref sp_mat_6_ang, ref sp_mat_6_mat, ref sp_mat_6_dyn, ref sp_mat_6_stat, ref sp_mat_6_bnc); break; }
                case MaterialType.Rubber:     { GUI_Material_Pres(ref sp_mat_7_dest, ref sp_mat_7_sol, ref sp_mat_7_dens, ref sp_mat_7_drag, ref sp_mat_7_ang, ref sp_mat_7_mat, ref sp_mat_7_dyn, ref sp_mat_7_stat, ref sp_mat_7_bnc); break; }
                case MaterialType.Ice:        { GUI_Material_Pres(ref sp_mat_8_dest, ref sp_mat_8_sol, ref sp_mat_8_dens, ref sp_mat_8_drag, ref sp_mat_8_ang, ref sp_mat_8_mat, ref sp_mat_8_dyn, ref sp_mat_8_stat, ref sp_mat_8_bnc); break; }
                case MaterialType.Wood:       { GUI_Material_Pres(ref sp_mat_9_dest, ref sp_mat_9_sol, ref sp_mat_9_dens, ref sp_mat_9_drag, ref sp_mat_9_ang, ref sp_mat_9_mat, ref sp_mat_9_dyn, ref sp_mat_9_stat, ref sp_mat_9_bnc); break; }
            }
            
            RFUI.SetDirty (man.gameObject);
        }
        
        void GUI_Material_Pres(
            ref SerializedProperty dest, 
            ref SerializedProperty sol, 
            ref SerializedProperty dens, 
            ref SerializedProperty drag, 
            ref SerializedProperty ang, 
            ref SerializedProperty mat, 
            ref SerializedProperty dyn, 
            ref SerializedProperty stat, 
            ref SerializedProperty bnc)
        {
            RFUI.PropertyField (dest, TextMan.gui_mat_dest);
            RFUI.IntSlider (sol, mat_sol_min, mat_sol_max, TextMan.gui_mat_sol);
            RFUI.Caption (TextMan.gui_cap_rb);
            RFUI.PropertyField (dens, TextMan.gui_mat_dens);
            RFUI.Slider (drag, mat_drag_min, mat_drag_max, TextMan.gui_mat_drag);
            RFUI.Slider (ang,  mat_ang_min,  mat_ang_max,  TextMan.gui_mat_ang);
            RFUI.Caption (TextMan.gui_cap_ph);
            RFUI.PropertyField (mat, TextMan.gui_mat_mat);
            RFUI.Slider (dyn,  mat_dyn_min, mat_dyn_max, TextMan.gui_mat_dyn);
            RFUI.Slider (stat, mat_dyn_min, mat_dyn_max, TextMan.gui_mat_stat);
            RFUI.Slider (bnc,  mat_dyn_min, mat_dyn_max, TextMan.gui_mat_bnc);
        }
        
        /// /////////////////////////////////////////////////////////
        /// Activation
        /// /////////////////////////////////////////////////////////

        void GUI_Activation()
        {
            RFUI.CaptionBox (TextMan.gui_cap_axt);
            RFUI.PropertyField (sp_act_par, TextMan.gui_act_par);
        }

        /// /////////////////////////////////////////////////////////
        /// Demolition
        /// /////////////////////////////////////////////////////////
        
        void UI_Demolition()
        {
            RFUI.CaptionBox (TextMan.gui_cap_dml);
            RFUI.Slider (sp_dml_sol,  solidity_min, solidity_max, TextMan.gui_dml_sol);
            RFUI.Slider (sp_dml_time, quota_min,    quota_max, TextMan.gui_dml_time);
            if (sp_dml_time.floatValue > 0)
                RFUI.PropertyField (sp_dml_quota, TextMan.gui_dml_quota);
            UI_Demolition_Adv();
        }

        void UI_Demolition_Adv()
        {
            RFUI.Foldout (ref fld_adv, TextKeys.man_fld_adv, TextMan.gui_adv_expand.text);
            if (fld_adv == true)
            {
                EditorGUI.indentLevel++;

                RFUI.Caption (TextMan.gui_cap_frg);
                RFUI.PropertyField (sp_adv_parent, TextMan.gui_adv_parent);
                if (man.adp.parent == FragmentParentType.GlobalParent)
                    RFUI.PropertyField (sp_adv_global, TextMan.gui_adv_global);
                RFUI.PropertyField (sp_adv_current, TextMan.gui_adv_current);
                RFUI.PropertyField (sp_adv_amount,  TextMan.gui_adv_amount);
                RFUI.Caption (TextMan.gui_cap_shad);
                RFUI.Slider (sp_adv_size, shadow_min, shadow_max, TextMan.gui_adv_size);

                EditorGUI.indentLevel--;
            }
        }

        /// /////////////////////////////////////////////////////////
        /// Pooling
        /// /////////////////////////////////////////////////////////
        
        void UI_Pooling()
        {
            RFUI.CaptionBox (TextMan.gui_cap_pol);
            RFUI.PropertyField (sp_pol_frg, TextMan.gui_pol_frg);
            if (man.fragments.enable == true)
            {
                RFUI.PropertyField (sp_pol_reu, TextMan.gui_pol_reu);
                RFUI.IntSlider (sp_pol_min, frag_cap_min, frag_cap_max, TextMan.gui_pol_min);
                if (man.fragments.reuse == true)
                    RFUI.IntSlider (sp_pol_max, frag_cap_min, frag_cap_max, TextMan.gui_pol_max);
            }

            EditorGUI.BeginChangeCheck();
            RFUI.PropertyField (sp_pol_prt, TextMan.gui_pol_prt);
            if (EditorGUI.EndChangeCheck() == true)
                man.particles.Enable = man.particles.enable;
            
            // Info
            if (man.particles.enable == true)
            {
                if (man.particles.emitters != null && man.particles.emitters.Count > 0)
                {
                    RFUI.Space ();
                    GUILayout.Label (TextMan.str_prt_emit + man.particles.emitters.Count,         EditorStyles.boldLabel);
                    RFUI.Space ();
                    GUILayout.Label (TextMan.str_prt_amount + man.particles.GetTotalPoolAmount(), EditorStyles.boldLabel);
                    RFUI.Space ();
                    GUILayout.Label (TextMan.str_prt_reu + man.particles.reused,                  EditorStyles.boldLabel);
                    RFUI.Space ();
                    GUILayout.Label (TextMan.str_prt_scene + man.particles.ResetCheck(),          EditorStyles.boldLabel);
                }
            }
        }

        /// /////////////////////////////////////////////////////////
        /// Info
        /// /////////////////////////////////////////////////////////

        void UI_Info()
        {
            // Disable for deactivated
            if (man.gameObject.activeSelf == false)
                return;
            
            RFUI.CaptionBox (TextMan.gui_cap_inf);

            if (RayfireMan.inst != null)
                GUILayout.Label (TextMan.str_inst + RayfireMan.inst.gameObject.name);
            
            RFUI.Space ();
            
            if (Application.isPlaying == true)
            {
                if (man.fragments.enable == true && man.fragments.queue != null && man.fragments.queue.Count > 0)
                    GUILayout.Label (TextMan.str_rigs + man.fragments.queue.Count);
                
                RFUI.Space ();
                
                if (man.adp.currentAmount > 0)
                    GUILayout.Label (TextMan.str_frags + man.adp.currentAmount + "/" + man.adp.maximumAmount);

                RFUI.Space ();

                if (man.physicList != null)
                    GUILayout.Label (TextMan.str_vel + man.physicList.Count);
                
                RFUI.Space ();

                if (man.fadeLiveList != null)
                    GUILayout.Label (TextMan.str_fade + man.fadeLiveList.Count);
                
                //RFUI.Space ();
                //GUILayout.Label ("Storage Roots: " + man.storage.storageRoots.Count);
                //GUILayout.Label ("Storage Frags: " + man.storage.storageFrags.Count);
            }
        }

        /// /////////////////////////////////////////////////////////
        /// About
        /// /////////////////////////////////////////////////////////
        
        void UI_About()
        {
            RFUI.CaptionBox (TextMan.gui_cap_abt);
            RFUI.PropertyField (sp_dbg_msg, TextMan.gui_dbg_msg);
            if (man.debugState == true)
            {
                RFUI.PropertyField (sp_dbg_edt, TextMan.gui_dbg_edt);
                RFUI.PropertyField (sp_dbg_bld, TextMan.gui_dbg_bld);
            }
            
            RFUI.HelpBox (TextMan.str_build + RayfireMan.buildMajor + '.' + RayfireMan.buildMinor.ToString ("D2"), MessageType.None, true);
            
            // GUILayout.Label (TextMan.str_v2 + Utils.GetBuildInfo());

            // Logo TODO remove if component removed
            if (logo == null)
                logo = (Texture2D)AssetDatabase.LoadAssetAtPath ("Assets/RayFire/Info/Logo/logo_small.png", typeof(Texture2D));
            if (logo != null)
                GUILayout.Box (logo, GUILayout.Width ((int)EditorGUIUtility.currentViewWidth - 19f), GUILayout.Height (64));
            
            if (GUILayout.Button (TextMan.gui_change, GUILayout.Height (20)))
                Application.OpenURL (TextMan.str_url);
        }
    }
}