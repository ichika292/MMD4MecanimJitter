using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MYB.MMD4MecanimJitter
{
    public class MorphJitImpl : MonoBehaviour
    {
        public MMD4MecanimModel _model;
        public bool sync;
        public bool overrideOnce;
        public List<MorphJitterHelper> helperList = new List<MorphJitterHelper>();

        public MorphJitterParameter loopParameter = new MorphJitterParameter(PrimitiveAnimationCurve.UpDown5, true);
        public MorphJitterParameter onceParameter = new MorphJitterParameter(PrimitiveAnimationCurve.UpDown1, false);

        protected List<Coroutine> loopRoutineList = new List<Coroutine>();
        protected List<Coroutine> onceRoutineList = new List<Coroutine>();
        protected Coroutine fadeInRoutine, fadeOutRoutine;

        //Editor用
        public bool loopGroupEnabled = true;
        public bool onceGroupEnabled = true;

        //コルーチンが動作中か否か
        public bool isProcessing
        {
            get {
                bool result = false;
                foreach (MorphJitterHelper h in helperList)
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
                foreach (MorphJitterHelper h in helperList)
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
            _model = GetComponentInParent<MMD4MecanimModel>();
            if (_model == null)
            {
                Debug.Log("MMD4MecanimModel not found.");
                return;
            }
        }

        void Awake()
        {
            foreach (MorphJitterHelper h in helperList)
                h.Initialize(this);
        }

        void Update()
        {
            if (!isProcessing) return;

            SetMorphWeight();
        }

        protected void SetMorphWeight()
        {
            float? weight = null;
            if (sync)
            {
                //helperList[0]のweight計算結果を全モーフで共有
                foreach (MorphJitterHelper h in helperList)
                {
                    if (!weight.HasValue)
                        weight = h.SetMorphWeight();
                    else
                        h.SetMorphWeight(weight.Value);
                }
            }
            else
            {
                foreach (MorphJitterHelper h in helperList)
                {
                    h.SetMorphWeight();
                }
            }
        }

        //Editor変更時
        protected void OnValidate()
        {
            loopParameter.AdjustParameter();
        }

        protected void ResetRoutineList(List<Coroutine> list)
        {
            foreach (Coroutine r in list)
                StopCoroutine(r);
            list.Clear();
        }

        protected void ResetAllLoopState()
        {
            foreach (MorphJitterHelper h in helperList)
                h.ResetLoopState();
        }

        protected void ResetAllOnceState()
        {
            foreach (MorphJitterHelper h in helperList)
                h.ResetOnceState();
        }

        /// <summary>
        /// MecanimModel.morphListからWeight>0のモーフを取得
        /// </summary>
        protected void SetMorph()
        {
            if (_model.morphList == null)
                _model.InitializeOnEditor();
            if (_model.morphList == null) return;

            ResetMorph();
            //morphWeight>0fのモーフ名を取得
            var list = _model.morphList.Where(x => x.weight > 0f);
            foreach (var morph in list)
                helperList.Add(new MorphJitterHelper(this, morph.name, morph.weight));

            if (helperList.Count() == 0)
                Debug.Log("Morph (Weight > 0) not found.");
        }

        /// <summary>
        /// helperListのリセット
        /// </summary>
        protected void ResetMorph()
        {
            foreach (MorphJitterHelper h in helperList)
            {
                h.loopState.isProcessing = false;
                h.onceState.isProcessing = false;
                h.morphWeight = 0f;
                h.morphName = "";
                h.UpdateMorph();
            }
            helperList.Clear();
            helperList.TrimExcess();
        }

        /// <summary>
        /// ループ再生用コルーチン
        /// </summary>
        /// <param name="state"></param>
        /// <returns></returns>
        protected IEnumerator LoopCoroutine(MorphJitterHelper.State state)
        {
            while (true)
            {
                state.isProcessing = true;
                
                state.SetNextParameter();

                state.timer = 0f;

                //loopGroupEnabled = falseの間は再生停止
                if (!loopGroupEnabled)
                {
                    state.isProcessing = false;
                    while (!loopGroupEnabled)
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

                //Interval
                float intervalTimer = 0f;
                while (intervalTimer < state.curInterval)
                {
                    intervalTimer += Time.deltaTime;
                    yield return null;
                }
            }
        }

        /// <summary>
        /// 1周再生用コルーチン
        /// </summary>
        protected IEnumerator OnceCoroutine(MorphJitterHelper.State state, System.Action callback)
        {
            if (!onceGroupEnabled) yield break;

            state.isProcessing = true;
            
            state.SetOnceParameter();

            state.timer = 0f;

            //Period
            while (state.timer < state.curPeriod)
            {
                state.timer += Time.deltaTime;
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

            if (!isProcessing) callback();
        }

        protected IEnumerator FadeInCoroutine(float sec)
        {
            sec = Mathf.Max(0.01f, sec);

            while (loopParameter.magnification < 1f)
            {
                loopParameter.magnification += Time.deltaTime / sec;
                yield return null;
            }
            loopParameter.magnification = 1f;
            fadeInRoutine = null;
        }

        protected IEnumerator FadeOutCoroutine(float sec, System.Action callback)
        {
            sec = Mathf.Max(0.01f, sec);

            while (loopParameter.magnification > 0f)
            {
                loopParameter.magnification -= Time.deltaTime / sec;
                yield return null;
            }
            loopParameter.magnification = 0f;
            fadeOutRoutine = null;
            loopGroupEnabled = false;
            callback();
        }


#if UNITY_EDITOR
        [CustomEditor(typeof(MorphJitImpl))]
        public class MorphJitterImplEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                EditorGUILayout.LabelField("Please attach \"MMD4M_MorphJitter\" insted of this.");
            }
        }
#endif
    }
}