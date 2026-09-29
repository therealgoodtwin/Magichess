using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace RayFire
{
    /// <summary>
    /// Rayfire Rigid reference demolition class.
    /// </summary>
    [Serializable]
    public class RFReferenceDemolition
    {
        public enum ActionType
        {
            Instantiate = 0,
            SetActive   = 1
        }

        // UI
        public GameObject       rfs;    // Reference
        public List<GameObject> rnd;    // Random references
        public ActionType       act;    // Action
        public bool             add;    // Add Rigid
        public bool             scl;    // Inherit scale
        public bool             mat;    // Inherit material
        
        /// /////////////////////////////////////////////////////////
        /// Constructor
        /// /////////////////////////////////////////////////////////
        
        // Constructor
        public RFReferenceDemolition()
        {
            InitValues();
        }
        
        void InitValues()
        {
            rfs = null;
            rnd = null;
            add = true;
            scl = true;
            mat = false;
        }
        
        // Pool Reset
        public void GlobalReset()
        {
            InitValues();
        }
        
        // Copy from
        public void CopyFrom (RFReferenceDemolition source)
        {
            rfs = source.rfs;
            rnd = source.rnd;
            add = source.add;
            scl = source.scl;
            mat = source.mat;
        }
        
        /// /////////////////////////////////////////////////////////
        /// Methods
        /// /////////////////////////////////////////////////////////   
        
        public bool HasRandomRefs { get { return rnd != null && rnd.Count > 0; } }
        
        // Get reference
        public GameObject GetReference()
        {
            // Return reference if action type is SetActive
            if (act == ActionType.SetActive)
            {
                // Return single ref
                if (rfs != null && HasRandomRefs == false)
                    return rfs;
                
                // Get random ref
                if (HasRandomRefs == true)
                    return rnd[Random.Range (0, rnd.Count)];
                
                // Reference not defined or destroyed
                if (rfs == null)
                    return null;
                
                // Reference is prefab asset
                if (rfs.scene.rootCount == 0)
                    return null;
                
                return rfs;
            }

            if (act == ActionType.Instantiate)
            {
                // Get random ref
                if (HasRandomRefs == true)
                    return rnd[Random.Range (0, rnd.Count)];
                
                // Return single ref
                if (rfs != null && HasRandomRefs == false)
                    return rfs;
            }
            
            return null;
        }
        
        // Demolish object to reference
        public static bool DemolishReference (RayfireRigid scr)
        {
            if (scr.dmlTp == DemolitionType.ReferenceDemolition)
            {
                // Demolished
                scr.lim.demolished = true;
                
                // Turn off original
                scr.gameObject.SetActive (false);
                
                // Get reference
                GameObject refGo = scr.refDemol.GetReference();

                // Has no reference
                if (refGo == null)
                    return true;

                // Check if reference has already initialized Rigid
                RayfireRigid refScr = refGo.gameObject.GetComponent<RayfireRigid>();
                if (refScr != null && refScr.initialized == true)
                {
                    RayfireMan.Log (RFLog.rig_dbgn + scr.name + RFLog.rig_init1, scr.gameObject);
                    return false;
                }
                
                // Check if reference has RigidRoot TODO optimize
                RayfireRigidRoot rootScr = null;
                if (refScr == null)
                {
                    rootScr = refGo.gameObject.GetComponent<RayfireRigidRoot>();
                    if (rootScr != null && rootScr.initialized == true)
                    {
                        RayfireMan.Log (RFLog.rig_dbgn + scr.name + RFLog.rig_init2, scr.gameObject);
                        return false;
                    }
                }
                
                // Set object to swap
                GameObject instGo = GetInstance (scr, refGo);

                // Set root to manager or to the same parent
                RayfireMan.SetParentByManager (instGo.transform, scr.tsf, false);
                
                // Set tm
                scr.rtC = instGo.transform;
                
                // Copy scale
                if (scr.refDemol.scl == true)
                    scr.rtC.localScale = scr.tsf.localScale;

                // Inherit materials
                InheritMaterials (scr, instGo);

                // Clear list for fragments
                scr.fragments = new List<RayfireRigid>();
                
                // Check root for rigid props
                RayfireRigid rigid = instGo.gameObject.GetComponent<RayfireRigid>();

                // Reference Root has not rigid. Add to
                AddRigid (rigid, instGo, scr);

                // Activate and init rigid
                instGo.transform.gameObject.SetActive (true);

                // Initialize rigidroot
                if (rootScr != null)
                {
                    instGo.gameObject.GetComponent<RayfireRigidRoot>().Initialize();
                }
                
                // Reference has rigid
                else if (rigid != null)
                {
                    // Init if not initialized yet
                    rigid.Initialize();
                    
                    // Create rigid for root children
                    if (rigid.objTp == ObjectType.MeshRoot)
                    {
                        // Collect referenced fragments
                        scr.fragments.AddRange (rigid.fragments);
                    }

                    // Get ref rigid
                    else if (rigid.objTp == ObjectType.Mesh || rigid.objTp == ObjectType.SkinnedMesh)
                    {
                        // Disable runtime caching
                        rigid.mshDemol.ch.tp = CachingType.Disabled;
                        
                        // Instance has no meshes
                        if (rigid.mFlt == null && rigid.skr == null)
                            return true;
                        
                        // Demolish mesh instance
                        RFDemolitionMesh.DemolishMesh(rigid);
                        
                        // Collect fragments
                        if (rigid.HasFragments == true)
                            scr.fragments.AddRange (rigid.fragments);
                        
                        // Destroy instance
                        RayfireMan.DestroyFragment (rigid, rigid.rtP, 1f);
                    }

                    // Get ref rigid
                    else if (rigid.objTp == ObjectType.NestedCluster || rigid.objTp == ObjectType.ConnectedCluster)
                    {
                        rigid.Default();
                        
                        // Copy contact data
                        RFLimitations.Copy(scr.lim, rigid.lim);
  
                        // Demolish
                        RFDemolitionCluster.DemolishCluster (rigid);
                        
                        // Collect new fragments
                        scr.fragments.AddRange (rigid.fragments);
                        
                        // Collect demolished cluster
                        if (rigid.clsDemol.cluster.shards.Count > 0)
                            scr.fragments.Add (rigid);
                    }
                }
                
                // Clean object
                else
                {
                    Rigidbody rb = instGo.GetComponent<Rigidbody>();
                    if (rb != null && scr.physics.rb != null)
                    {
                        rb.linearVelocity        = scr.physics.rb.linearVelocity;
                        rb.angularVelocity = scr.physics.rb.angularVelocity;
                    }
                }
            }

            return true;
        }

        // Add rigid if has no
        static void AddRigid(RayfireRigid rigid, GameObject instGo, RayfireRigid scr)
        {
            // Reference Root has not rigid. Add to
            if (rigid == null && scr.refDemol.add == true)
            {
                // Add rigid and copy
                rigid = instGo.gameObject.AddComponent<RayfireRigid>();

                // Copy rigid
                scr.CopyPropertiesTo (rigid);

                // Disable runtime demolition for default rigid
                rigid.dmlTp = DemolitionType.None;

                // Set fragments sim type
                RFPhysic.SetFragmentSimulationType (rigid, scr.simTp);
                    
                // Copy particles from demolished rigid to instanced rigid
                RFPoolingParticles.CopyParticlesRigid (scr, rigid);   
                    
                // Single mesh
                if (instGo.transform.childCount == 0)
                {
                    rigid.objTp = ObjectType.Mesh;
                }

                // Multiple meshes
                if (instGo.transform.childCount > 0)
                {
                    rigid.objTp = ObjectType.MeshRoot;
                }
            }
        }
        
        // Get final instance accordingly to action type
        static GameObject GetInstance (RayfireRigid scr, GameObject refGo)
        {
            GameObject instGo;

            // Check if reference is prefab
            if (scr.refDemol.act == ActionType.SetActive)
            {
                if (refGo.gameObject.scene.path == null)
                {
                    RayfireMan.Log (RFLog.rig_dbgn + scr.name + RFLog.rig_ref, scr.gameObject);
                    scr.refDemol.act = ActionType.Instantiate;
                }
            }

            // Instantiate turned off reference with null parent
            if (scr.refDemol.act == ActionType.Instantiate)
            {
                instGo = Object.Instantiate (refGo, scr.tsf.position, scr.tsf.rotation);
                instGo.name = refGo.name;
            }
                
            // Set active
            else
            {
                instGo                    = refGo;
                instGo.transform.position = scr.tsf.position;
                instGo.transform.rotation = scr.tsf.rotation;
            }
            
            return instGo;
        }

        // Inherit materials from original object to referenced fragments TODO consider different inner material for fragments
        static void InheritMaterials (RayfireRigid scr, GameObject instGo)
        {
            if (scr.refDemol.mat == true)
            {
                Renderer[] renderers = instGo.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                    for (int r = 0; r < renderers.Length; r++)
                        renderers[r].sharedMaterials = scr.mRnd.sharedMaterials;
            }
        }
    }
}