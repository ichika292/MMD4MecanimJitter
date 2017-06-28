using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MYB.MMD4MecanimJitter
{
    public class BoneJitImpl : MonoBehaviour
    {
        public MMD4MecanimBone bone;
        public bool syncAxis = false;
        public bool overrideOnce;
        public float angleMagnification = 10f;
        public float maxDegreesDelta = 1f;
        public List<MMD4M_BoneJitter> children = new List<MMD4M_BoneJitter>();
        public List<BoneJitterHelper> helperList = new List<BoneJitterHelper>();

        public BoneJitterParameter[] loopParameter =
        {
            new BoneJitterParameter(PrimitiveAnimationCurve.Cos, true, true),
            new BoneJitterParameter(PrimitiveAnimationCurve.Sin, true),
            new BoneJitterParameter(PrimitiveAnimationCurve.Sin, true)
        };

        public BoneJitterParameter[] onceParameter =
        {
            new BoneJitterParameter(PrimitiveAnimationCurve.UpDown25, false, true),
            new BoneJitterParameter(PrimitiveAnimationCurve.UpDown25, false),
            new BoneJitterParameter(PrimitiveAnimationCurve.UpDown25, false)
        };

        protected Animator anim;
        protected Vector2 magnification = Vector2.one;    //振幅倍率 x:Loop y:Once
        protected List<Coroutine> loopRoutineList = new List<Coroutine>();
        protected List<Coroutine> onceRoutineList = new List<Coroutine>();
        protected Coroutine fadeInRoutine, fadeOutRoutine;

        //Editor用
        public string[] axisLabel = { "--- X ---", "--- Y ---", "--- Z ---" };
        public bool loopGroupEnabled = true;
        public bool onceGroupEnabled = true;
        public bool[] loopEnabled = { true, true, true };
        public bool[] onceEnabled = { true, true, true };

        public bool isChild;

        //コルーチンが動作中か否か
        public bool isProcessing
        {
            get {
                bool result = false;
                foreach (BoneJitterHelper h in helperList)
                {
                    if (h.isProcessing)
                    {
                        result = true;
                        break;
                    }
                }
                return result;
            }
        }
        
        //Onceコルーチンが動作中か否か
        public bool OnceIsProcessing
        {
            get {
                bool result = false;
                foreach (BoneJitterHelper h in helperList)
                {
                    if (h.OnceIsProcessing)
                    {
                        result = true;
                        break;
                    }
                }
                return result;
            }
        }

        void Reset()
        {
            try
            {
                //1階層上までMecanimBoneを検索
                bone = GetComponent<MMD4MecanimBone>();

                if (bone == null)
                    bone = transform.parent.GetComponent<MMD4MecanimBone>();

                //MecanimBone階層下から、同Boneを操作対象としたBoneJitter(親)が存在するか確認
                int parentBoneJitter = bone.GetComponentsInChildren<MMD4M_BoneJitter>()
                                            .Where(x => x.bone == this.bone)
                                            .Where(x => x.isChild == false)
                                            .Count(x => x.GetInstanceID() != this.GetInstanceID());

                //親が既に在る場合、ループ再生を無効
                if (parentBoneJitter > 0)
                {
                    isChild = true;
                    loopGroupEnabled = false;
                    for (int i = 0; i < 3; i++)
                        loopEnabled[i] = false;
                }
            }
            catch (System.Exception)
            {
                Debug.Log("MMD4MecanimBone not found.");
            }
        }

        void Awake()
        {
            if (bone == null) return;

            anim = bone.model.GetComponent<Animator>();

            if (!isChild)
            {
                children = bone.GetComponentsInChildren<MMD4M_BoneJitter>()
                    .Where(x => (x.bone == this.bone) && (x.GetInstanceID() != this.GetInstanceID()))
                    .ToList();
            }

            //helperList初期化 (syncAxis ? 1 : 3)個インスタンス化
            for (int i = 0; i < (syncAxis ? 1 : 3); i++)
            {
                helperList.Add(new BoneJitterHelper(this, loopParameter[i], onceParameter[i]));
            }
        }

        void LateUpdate()
        {
            if (anim == null) return;
            if (!isProcessing) return;

            SetUserRotation();
        }

        //Editor変更時
        protected void OnValidate()
        {
            for (int i = 0; i < 3; i++)
            {
                loopParameter[i].isEnabled = loopEnabled[i];
                onceParameter[i].isEnabled = onceEnabled[i];
                loopParameter[i].syncAxis = syncAxis;
                onceParameter[i].syncAxis = syncAxis;
            }

            foreach (BoneJitterParameter param in loopParameter)
                param.AdjustParameter();
        }

        protected void ResetRoutineList(List<Coroutine> list)
        {
            foreach (Coroutine r in list)
                StopCoroutine(r);
            list.Clear();
        }

        protected void ResetAllLoopState()
        {
            foreach (BoneJitterHelper h in helperList)
                h.ResetLoopState();
        }

        protected void ResetAllOnceState()
        {
            foreach (BoneJitterHelper h in helperList)
                h.ResetOnceState();
        }

        //EulerAngle集計 & セット
        protected void SetUserRotation()
        {
            Vector3 vec = GetEulerAngle();

            foreach (MMD4M_BoneJitter child in children)
                vec += child.GetEulerAngle();

            var rot = Quaternion.Euler(vec * angleMagnification);
            
            if (anim.runtimeAnimatorController == null)
                bone.transform.localRotation = rot;
            else
                bone.transform.localRotation *= rot;
        }

        protected void ResetUserRotation()
        {
            //2行目だけではInspector上の数値が0にならない為
            bone.userRotation = new Quaternion();
            bone.userRotation = Quaternion.identity;
        }

        protected Vector3 GetEulerAngle()
        {
            Vector3 vec = Vector3.zero;

            //syncAxis = true の場合、X軸のパラメータ(helperList[0])から計算
            for (int i = 0; i < 3; i++)
            {
                var loopCurve = loopParameter[i].periodToAmplitude;
                var onceCurve = onceParameter[i].periodToAmplitude;

                Vector2 weight = helperList[syncAxis ? 0 : i].GetEulerAngle(loopCurve, onceCurve);
                Vector2 enabledFlag = new Vector2(loopEnabled[i] ? 1 : 0, onceEnabled[i] ? 1 : 0);

                vec[i] = Vector2.Dot(Vector2.Scale(weight, magnification), enabledFlag);
            }

            return vec;
        }
        
        /// <summary>
        /// ループ再生用コルーチン
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        protected IEnumerator LoopCoroutine(BoneJitterHelper.State state)
        {
            while (true)
            {
                state.isProcessing = true;
                
                state.SetNextParameter();

                state.timer = 0f;

                //各軸isEnabled = falseの間は再生停止
                var param = state.param;
                if (!param.isEnabled && !param.isXAxis && param.syncAxis)
                {
                    state.isProcessing = false;
                    while (!param.isEnabled)
                    {
                        yield return null;
                    }
                    state.isProcessing = true;
                }

                //Period
                while (state.timer < 1f)
                {
                    state.timer += Time.deltaTime / state.GetCurrentPeriod();
                    yield return null;
                }
            }
        }

        /// <summary>
        /// 1周再生用コルーチン
        /// </summary>
        protected IEnumerator OnceCoroutine(BoneJitterHelper.State state)
        {
            if (!onceGroupEnabled) yield break;

            state.isProcessing = true;
            
            state.SetOnceParameter();

            state.timer = 0f;

            //Period
            while (state.timer < 1f)
            {
                state.timer += Time.deltaTime / state.GetCurrentPeriod();
                yield return null;
            }

            //Interval
            float intervalTimer = 0f;
            while (intervalTimer < state.curInterval)
            {
                intervalTimer += Time.deltaTime;
                yield return null;
            }

            state.timer = 0f;
            yield return null;
            state.isProcessing = false;
        }

        protected IEnumerator FadeInCoroutine(float sec)
        {
            sec = Mathf.Max(0.01f, sec);

            while(magnification.x < 1f)
            {
                magnification.x += Time.deltaTime / sec;
                yield return null;
            }
            magnification.x = 1f;
            fadeInRoutine = null;
        }

        protected IEnumerator FadeOutCoroutine(float sec, System.Action callback)
        {
            sec = Mathf.Max(0.01f, sec);

            while (magnification.x > 0f)
            {
                magnification.x -= Time.deltaTime / sec;
                yield return null;
            }
            magnification.x = 0f;
            fadeOutRoutine = null;
            loopGroupEnabled = false;
            callback();
        }

#if UNITY_EDITOR
        //Inspector拡張クラス
        [CustomEditor(typeof(BoneJitImpl))]
        public class BoneJitterImplEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                EditorGUILayout.LabelField("Please attach \"MMD4M_BoneJitter\" insted of this.");
            }
        }
#endif
    }
}