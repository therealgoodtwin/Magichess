using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RayFire
{
    /// <summary>
    /// Rayfire Recorder animation cache class.
    /// </summary>
    [Serializable]
    public class RFCache
    {
        // Vars
        public string           name;
        public List<bool>       act;
        public List<Vector3>    pos;
        public List<Quaternion> rot;
        public Transform        trm;

        // Constructor
        public RFCache (Transform recRootTm, Transform tm, int offset = 0)
        {
            trm  = tm;
            name = tm.name;
            act  = new List<bool>();
            pos  = new List<Vector3>();
            rot  = new List<Quaternion>();
            
            // Add activation offset to lists
            SetOffset (this, offset);
            
            // Not child of main recorder root. Name should include all parents names up to main recorder root
            if (tm.parent != recRootTm)
            {
                Transform lastParent = tm.parent;
                if (lastParent != null)
                {
                    while (recRootTm != lastParent)
                    {
                        name       = name.Insert (0, "/");
                        name       = name.Insert (0, lastParent.name);
                        lastParent = lastParent.parent;
                    }
                }
            }
        }

        // Set activation offset 
        static void SetOffset(RFCache cache, int offset)
        {
            if (offset <= 0)
                return;
            
            for (int i = 0; i < offset; i++)
            {
                cache.act.Add (false);
                cache.pos.Add (cache.trm.localPosition);
                cache.rot.Add (cache.trm.localRotation);
            }
        }
    }

    [SelectionBase]
    [DisallowMultipleComponent]
    [AddComponentMenu (RFLog.rec_path)]
    [HelpURL (RFLog.rec_link)]
    public class RayfireRecorder : MonoBehaviour
    {
        public enum AnimatorType
        {
            Disabled = 0,
            Record   = 2,
            Play     = 8
        }

        public enum RigidActionType
        {
            Disable      = 0,
            SetKinematic = 2
        }
        
        // Main
        public AnimatorType mode = AnimatorType.Record;

        // Record
        public bool   recordOnStart = true;
        public string clipName;
        public float  duration   = 5f;
        public int    rate       = 15;
        public bool   reduceKeys = true;
        public float  threshold;
        public bool   demolition;

        // Playback
        public bool                      playOnStart;
        public float                     speed = 1f;
        public AnimationClip             animationClip;
        public RuntimeAnimatorController controller;
        public RigidActionType           rigidAction;
        
        
        public float playMark;
        
        
        [NonSerialized] public bool playState;
        [NonSerialized] public bool pauseState;
        
        // Public Non Serialized
        [NonSerialized] public bool  recorder;
        [NonSerialized] public float recordedTime;

        // Private Non Serialized
        [NonSerialized] public List<GameObject>   pfList;
        [NonSerialized]        string             assetFolder;
        [NonSerialized]        float              stepTime;
        [NonSerialized] public Animator           animator;
        [NonSerialized]        List<Transform>    tmList;
        [NonSerialized]        List<RFCache>      cacheList;
        [NonSerialized]        List<float>        timeList;
        [NonSerialized]        List<RayfireRigid> rigids;
        
        // Static
        string recordFolder = "RayFireRecords/";
        
        /// //////////////////////////////////////////////////
        /// Common
        /// //////////////////////////////////////////////////

        // Awake
        void Awake()
        {
            // Set vars
            SetVariables();
        }

        // Start
        void Start()
        {
            // Collect rigid
            SetRigidPlay();

            // Start ops
            if (mode == AnimatorType.Record && recordOnStart == true)
                StartRecord();
            else if (mode == AnimatorType.Play && playOnStart == true)
                StartPlay();
        }

        // Set vars
        void SetVariables()
        {
            if (mode != AnimatorType.Disabled)
            {
                animator = GetComponent<Animator>();

                // Get list of cached transforms
                tmList = gameObject.GetComponentsInChildren<Transform> (false).ToList();
                tmList.Remove (transform);

                // No children
                if (tmList.Count == 0)
                {
                    RayfireMan.Log (RFLog.rec_dbgn + name + RFLog.rec_md1 + mode.ToString() + RFLog.rec_noChld, gameObject);
                    mode = AnimatorType.Disabled;
                    return;
                }

                // Record set
                SetModeRecord();

                // Play set
                SetModePlay();
            }
        }
        
        // Play set
        void SetModePlay()
        {
            if (mode == AnimatorType.Play)
            {
                // Check for null controller
                if (controller == null)
                {
                    RayfireMan.Log (RFLog.rec_dbgn + name + RFLog.rec_md1 + mode.ToString() + RFLog.rec_noCont, gameObject);
                    mode = AnimatorType.Disabled;
                    return;
                }

                // Check for null controller
                if (animationClip == null)
                {
                    RayfireMan.Log (RFLog.rec_dbgn + name + RFLog.rec_md1 + mode.ToString() + RFLog.rec_noClip, gameObject);
                    mode = AnimatorType.Disabled;
                    return;
                }

                // Check for clip in controller
                bool hasClip = false;
                foreach (var anim in controller.animationClips)
                    if (anim == animationClip)
                        hasClip = true;
                if (hasClip == false)
                {
                    RayfireMan.Log (RFLog.rec_dbgn + name + RFLog.rec_md1 + mode.ToString() + RFLog.rec_noAnim, gameObject);
                    mode = AnimatorType.Disabled;
                    return;
                }

                // Create animator
                if (animator == null)
                    animator = gameObject.AddComponent<Animator>();
                        
                        
                        
                #if UNITY_6000_0_OR_NEWER

                animator.updateMode = AnimatorUpdateMode.Fixed;
                
                 /*
                 error CS0619: 'AnimatorUpdateMode.AnimatePhysics' is obsolete: 'Enum member AnimatorUpdateMode.AnimatePhysics 
                 has been deprecated. Use AnimatorUpdateMode.Fixed to evaluate in FixedUpdate time 
                 and Animator.animatePhysics to sync transforms for physics. (UnityUpgradable) -> Fixed'
                 */
                
                #else
                animator.updateMode = AnimatorUpdateMode.AnimatePhysics;
                #endif


                // Set defined controller
                animator.runtimeAnimatorController = controller;
            }
        }

        /// //////////////////////////////////////////////////
        /// Record
        /// //////////////////////////////////////////////////

        // Record set
        void SetModeRecord()
        {
            if (mode == AnimatorType.Record)
            {
                // Null active controller 
                if (animator != null)
                    animator.runtimeAnimatorController = null;

                // Prepare cache list
                if (tmList.Count > 0)
                {
                    cacheList = new List<RFCache>();
                    for (int i = 0; i < tmList.Count; i++)
                        cacheList.Add (new RFCache (transform, tmList[i]));
                }
                
                // Time list
                timeList = new List<float>();
                
                // Clip folder
                assetFolder = "Assets/" + recordFolder;
                
                // Rigid Runtime Demolition
                if (demolition == true)
                    SetRigidRecord();
            }
        }
        
        // Start record
        public void StartRecord()
        {
            // Stop
            if (cacheList.Count == 0)
                return;
            
            // Set demolition parent to parent of demolished object in order to record animation clip
            if (RayfireMan.inst != null && demolition == true)
            {
                RayfireMan.inst.adp.parent       = FragmentParentType.GlobalParent;
                RayfireMan.inst.adp.globalParent = transform;
            }

            // Start recording cor
            StartCoroutine (RecordCor());
        }

        // Stop record
        public void StopRecord()
        {
            recorder = false;
        }
        
        // Reset
        void Reset()
        {
            clipName = gameObject.name;
        }
        
        // Record tm every frame
        IEnumerator RecordCor()
        {
            // Set time step
            stepTime = 1.0f / rate;
            
            // Set the playback framerate. IMPORTANT: use for smooth keys recording
            Time.captureDeltaTime = stepTime;
            
            recorder = true;
            while (recorder == true)
            {
                // Save data
                timeList.Add (recordedTime);
                CacheFrame();

                // Set time
                recordedTime += stepTime;
 
                // Temp
                if (duration > 0 && recordedTime > duration)
                    StopRecord();
                
                // Wait
                yield return new WaitForSeconds (stepTime);
            }
            
            #if UNITY_EDITOR
            
            // Create clip 
            RFRecorder.CreateAnimationClip (cacheList, timeList, threshold, rate, assetFolder, clipName, reduceKeys);
            
            // Destroy prefab components
            RFRecorder.DestroyPrefabComponents(pfList);
            #endif
        }
        
        // Cache frame data
        void CacheFrame()
        {
            for (int i = 0; i < tmList.Count; i++)
            {
                if (tmList[i] == null)
                    continue;
                    
                cacheList[i].act.Add (tmList[i].gameObject.activeSelf);
                cacheList[i].pos.Add (tmList[i].localPosition);
                cacheList[i].rot.Add (tmList[i].localRotation);
            }
        }

        /// //////////////////////////////////////////////////
        /// Play
        /// //////////////////////////////////////////////////
        
        public void StartPlay()
        {
            if (animator == null)
                return;
                
            if (mode == AnimatorType.Play && playState == false)
            {
                playState      = true;
                animator.speed = Speed;
                animator.Play (animationClip.name);
            }
        }

        public void Pause(bool PauseState)
        {
            if (animator == null)
                return;
            
            if (mode == AnimatorType.Play)
            {
                animator.speed = PauseState == true ? 0f : Speed;
                pauseState     = PauseState;
            }
        }
        
        public void Restart (float RestartState)
        {
            if (animator == null)
                return;
            
            if (mode == AnimatorType.Play)
            {
                animator.Play (animationClip.name, -1, RestartState);
                pauseState     = false;
                animator.speed = Speed;
            }
        }
        
        public void Mark (float MarkState)
        {
            if (animator == null)
                return;
            
            if (mode == AnimatorType.Play)
            {
                pauseState     = true;
                animator.speed = 0f;
                animator.Play (animationClip.name, -1, MarkState);
            }
        }
        
        public void ResetPlay ()
        {
            if (animator == null)
                return;
            
            if (mode == AnimatorType.Play)
            {
                animator.Play (animationClip.name, -1, 0f);
                pauseState     = true;
                animator.speed = 0f;
            }
        }
        
        /// //////////////////////////////////////////////////
        /// Getters
        /// //////////////////////////////////////////////////
        
        // Get Destructible state
        public float Speed
        {
            get 
            { 
                if (speed < 0)
                    speed = 0f;
                return speed; 
            }
            set 
            {
                speed = value;
                if (speed < 0)
                    speed = 0f;
                if (mode == AnimatorType.Play && Application.isPlaying == true)
                    if (animator != null)
                        animator.speed = speed;
            }
        }
        
        /// //////////////////////////////////////////////////
        /// Rigid
        /// //////////////////////////////////////////////////
        
        // Set rigid props
        void SetRigidRecord()
        {
            // Destroy lists
            pfList = new List<GameObject>();
            
            // Get all Rigids
            rigids = gameObject.GetComponentsInChildren<RayfireRigid>().ToList();
            
            // Setup
            PrepareRigidRecord (rigids);
        }

        // Prepare Rigid for record
        void PrepareRigidRecord(List<RayfireRigid> rigidList)
        {
            for (int i = 0; i < rigidList.Count; i++)
            {
                if (rigidList[i].dmlTp == DemolitionType.Runtime || 
                    rigidList[i].dmlTp == DemolitionType.AwakePrecache || 
                    rigidList[i].dmlTp == DemolitionType.AwakePrefragment)
                {
                    // Used by Recorder record
                    rigidList[i].physics.rec = true;
                    
                    // Do not destroy after demolition
                    rigidList[i].reset.act     = RFReset.PostDemolitionType.DeactivateToReset;
                    rigidList[i].lim.desc = new List<RayfireRigid>();
                    
                    // Subscribe to demolition
                    #if UNITY_EDITOR
                    
                    rigidList[i].demolitionEvent.LocalEvent += RigidDemolition;
                    
                    // Set global parent
                    RayfireMan.inst.adp.parent       = FragmentParentType.GlobalParent;
                    RayfireMan.inst.adp.globalParent = transform;
                    
                    #endif                    
                }
            }
        }
        
        // Set rigid props
        void SetRigidPlay()
        {
            if (mode == AnimatorType.Play)
            {
                rigids = gameObject.GetComponentsInChildren<RayfireRigid>().ToList();
                foreach (RayfireRigid rigid in rigids)
                {
                    if (rigid.physics.exclude == false)
                    {
                        // Used by Recorder playback
                        rigid.physics.rec = true;

                        // Disable Rigid
                        if (rigidAction == RigidActionType.Disable)
                        {
                            rigid.enabled = false;
                        }
                        
                        // Check for kinematic state
                        else if (rigidAction == RigidActionType.SetKinematic)
                        {
                            rigid.simTp = SimType.Kinematic;
                            if (rigid.physics.rb != null)
                                RFPhysic.SetSimulationType (rigid.physics.rb, rigid.simTp, rigid.objTp, rigid.physics.gr, rigid.physics.si, rigid.physics.st);
                        }
                    }
                }
            }
        }

        /// //////////////////////////////////////////////////
        /// Demolition event
        /// //////////////////////////////////////////////////
        
        // Runtime demolition ops
        void RigidDemolition(RayfireRigid rigid)
        {
            // Mesh to fragments demolition
            if (rigid.objTp == ObjectType.Mesh)
            {
                // Collect root and fragments to continue record animation
                tmList.Add (rigid.rtC);
                cacheList.Add (new RFCache (transform, rigid.rtC, timeList.Count));
                for (int i = 0; i < rigid.fragments.Count; i++)
                {
                    tmList.Add (rigid.fragments[i].transform);
                    cacheList.Add (new RFCache (transform, rigid.fragments[i].transform, timeList.Count));
                }
                
                // Prepare new Rigids for secondary demolition record
                PrepareRigidRecord (rigid.fragments);

                // Export fragments meshes into asset
                #if UNITY_EDITOR
                RFRecorder.ExportAssets (rigid, this);
                #endif   
            }
        }
    }
}