using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MYB.MMD4MecanimJitter
{
    /// <summary>
    /// LoopとOnceのStateを管理する。
    /// ReorderableListで設定されたMorphと同数がインスタンス化され、同数のコルーチンが走る。
    /// </summary>
    [System.Serializable]
    public class MorphJitterHelper
    {
        /// <summary>
        /// 再生中に変動するパラメータ群
        /// </summary>
        public class State
        {
            public MorphJitterParameter param;
            public bool isProcessing;
            public float timer;         //周期毎にリセットするカウンタ
            public float curPeriod;     //周期(秒)
            public float nextPeriod;
            public float curInterval;   //次周期までの待ち時間(秒)
            public float curAmplitude;  //morphWeight振幅
            public float nextAmplitude;
            public float curOffset;     //morphWeight下限
            public float nextOffset;

            public State(MorphJitterParameter _param)
            {
                param = _param;
                nextAmplitude = param.amplitude.Random();
                nextOffset = param.offset.Random();

                SetNextParameter();
            }

            public void SetNextParameter()
            {
                curPeriod = nextPeriod;
                nextPeriod = param.period.Random();

                curInterval = param.interval.Random();

                curAmplitude = nextAmplitude;
                nextAmplitude = param.amplitude.Random();

                curOffset = nextOffset;
                nextOffset = param.offset.Random();
            }

            public void SetOnceParameter()
            {
                curPeriod = param.period.Random();
                curInterval = param.interval.Random();
                curAmplitude = param.amplitude.Random();
                curOffset = param.offset.Random();
            }

            /// <summary>
            /// 現在のWeightを計算
            /// </summary>
            /// <returns>weight</returns>
            public float GetCurrentWeight()
            {
                if (curPeriod <= 0f) return curOffset;

                float timer01 = Mathf.Clamp01(timer);
                float amp = CalcBlendState(curAmplitude, nextAmplitude, timer01, param.blendNextAmplitude);
                float ofs = CalcBlendState(curOffset, nextOffset, timer01, param.blendNextAmplitude);
                float weight = Mathf.Clamp01(param.periodToAmplitude.Evaluate(timer01) * amp + ofs);
                return weight * param.magnification;
            }

            public float GetCurrentPeriod()
            {
                float timer01 = Mathf.Clamp01(timer);
                return CalcBlendState(curPeriod, nextPeriod, timer01, param.blendNextPeriod);
            }

            public void Reset()
            {
                isProcessing = false;
                timer = 0f;
            }
        }

        public MorphJitImpl manager;
        public string morphName;
        public float weightMagnification = 1f;
        public float morphWeight;
        public bool overrideWeight;

        protected MMD4MecanimModel _model;
        MMD4MecanimModel.Morph _modelMorph;

        public State loopState;
        public State onceState;

        public bool isProcessing { get { return loopState.isProcessing || onceState.isProcessing; } }
        public bool OnceIsProcessing { get { return onceState.isProcessing; } }

        public MorphJitterHelper(MorphJitImpl manager, string morphName, float weightMagnification)
        {
            Initialize(manager);
            this.morphName = morphName;
            this.weightMagnification = weightMagnification;
        }

        public void Initialize(MorphJitImpl manager)
        {
            this.manager = manager;
            this._model = manager._model;
            loopState = new State(manager.loopParameter);
            onceState = new State(manager.onceParameter);
        }

        public void ResetState()
        {
            ResetLoopState();
            ResetOnceState();
            morphWeight = 0f;
        }

        public void ResetLoopState()
        {
            loopState.Reset();
        }

        public void ResetOnceState()
        {
            onceState.Reset();
        }

        /// <summary>
        /// morphWeightをMMD4MecanimModel.Morphに適用
        /// </summary>
        public void UpdateMorph()
        {
            if (_model != null)
            {
                if (_modelMorph == null)
                    _modelMorph = _model.GetMorph(morphName);
                else if (_modelMorph.name != morphName)
                    _modelMorph = _model.GetMorph(morphName);
            }

            if (_modelMorph != null)
            {
                _modelMorph.weight = morphWeight * weightMagnification;
                _modelMorph.weight2 = overrideWeight ? 1.0f : 0.0f;
            }
        }

        /// <summary>
        /// LoopとOnceのStateからそれぞれmorphWeightを計算
        /// </summary>
        public float SetMorphWeight()
        {
            var weight = 0f;

            if (manager.loopGroupEnabled)
                weight += loopState.GetCurrentWeight();

            if (manager.onceGroupEnabled)
                weight += onceState.GetCurrentWeight();

            morphWeight = Mathf.Clamp01(weight);
            UpdateMorph();
            return weight;
        }

        /// <summary>
        /// 数値指定でweightを適用
        /// </summary>
        /// <param name="weight"></param>
        public void SetMorphWeight(float weight)
        {
            morphWeight = Mathf.Clamp01(weight);
            UpdateMorph();
        }

        /// <summary>
        /// 次周期のパラメータとの補間(AmplitudeとOffset)
        /// </summary>
        static float CalcBlendState(float current, float next, float t, MorphJitterParameter.BlendState blendState)
        {
            switch (blendState)
            {
                case MorphJitterParameter.BlendState.Linear:
                    return Mathf.Lerp(current, next, t);
                case MorphJitterParameter.BlendState.Curve:
                    return (next - current) * (-2 * t + 3) * t * t + current;
                default:
                    return current;
            }
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(MorphJitterHelper))]
    public class MorphJitterHelperDrawer : PropertyDrawer
    {
        const int CLEARANCE_X = 4;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            using (new EditorGUI.PropertyScope(position, label, property))
            {
                //各プロパティー取得
                var nameProperty = property.FindPropertyRelative("morphName");
                var weightProperty = property.FindPropertyRelative("morphWeight");
                var magnificationProperty = property.FindPropertyRelative("weightMagnification");

                //表示位置を調整
                var nameRect = new Rect(position)
                {
                    width = position.width / 5f
                };

                var weightRect = new Rect(nameRect)
                {
                    x = nameRect.x + nameRect.width + CLEARANCE_X,
                    width = nameRect.width
                };

                var magnificationRect = new Rect(weightRect)
                {
                    x = weightRect.x + weightRect.width + CLEARANCE_X,
                    width = weightRect.width * 3 - CLEARANCE_X * 2
                };

                //Morph Name
                EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
                {
                    nameProperty.stringValue = EditorGUI.DelayedTextField(nameRect, nameProperty.stringValue);
                }
                EditorGUI.EndDisabledGroup();

                //Morph Weight
                var weight = weightProperty.floatValue * magnificationProperty.floatValue;
                EditorGUI.ProgressBar(weightRect, weight, weight.ToString("F2"));

                //Weight Magnification
                magnificationProperty.floatValue = EditorGUI.Slider(magnificationRect, magnificationProperty.floatValue, 0f, 1f);
            }
        }
    }
#endif
}