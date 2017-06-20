using UnityEngine;

namespace MYB.MMD4MecanimJitter
{
    /// <summary>
    /// LoopとOnceのStateを管理する。
    /// (syncAxis ? 1 : 3)個インスタンス化され、同数のコルーチンが走る。
    /// </summary>
    [System.Serializable]
    public class BoneJitterHelper
    {
        /// <summary>
        /// 再生中に変動するパラメータ群
        /// </summary>
        public class State
        {
            public BoneJitterParameter param;
            public bool isProcessing;
            public float timer;         //周期毎にリセットするカウンタ
            public float curPeriod;     //周期(秒)
            public float nextPeriod;
            public float curInterval;   //次周期までの待ち時間(秒)
            public float curAmplitude;  //angleWeight振幅
            public float nextAmplitude;
            public float curOffset;     //angleWeight下限
            public float nextOffset;

            public State(BoneJitterParameter _param)
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

            /// <summary>
            /// 現在のWeightを計算
            /// </summary>
            /// <returns>weight</returns>
            public float GetCurrentWeight(AnimationCurve curve)
            {
                if (curPeriod <= 0f) return curOffset;

                float timer01 = Mathf.Clamp01(timer);
                float amp = CalcBlendState(curAmplitude, nextAmplitude, timer01, param.blendNextAmplitude);
                float ofs = CalcBlendState(curOffset, nextOffset, timer01, param.blendNextAmplitude);
                return Mathf.Clamp(curve.Evaluate(timer01) * amp + ofs, -1, 1);
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

        public BoneJitImpl manager;
        public MMD4MecanimBone bone;
        
        public State loopState;
        public State onceState;

        public bool isProcessing { get { return loopState.isProcessing || onceState.isProcessing; } }
        public bool OnceIsProcessing { get { return onceState.isProcessing; } }

        public BoneJitterHelper(BoneJitImpl manager, BoneJitterParameter loopParameter, BoneJitterParameter onceParameter)
        {
            this.manager = manager;
            this.bone = manager.bone;

            loopState = new State(loopParameter);
            onceState = new State(onceParameter);
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
        /// LoopとOnceのStateからそれぞれeulerAngleWeightを計算
        /// x : Loop / y : Once
        /// </summary>
        public Vector2 GetEulerAngle(AnimationCurve loopCurve, AnimationCurve onceCurve)
        {
            Vector2 weight = Vector2.zero;

            if (manager.loopGroupEnabled)
                weight.x = loopState.GetCurrentWeight(loopCurve);

            if (manager.onceGroupEnabled)
                weight.y = onceState.GetCurrentWeight(onceCurve);

            return weight;
        }

        /// <summary>
        /// 次周期のパラメータとの補間(AmplitudeとOffset)
        /// </summary>
        static float CalcBlendState(float current, float next, float t, BoneJitterParameter.BlendState blendState)
        {
            switch (blendState)
            {
                case BoneJitterParameter.BlendState.Linear:
                    return Mathf.Lerp(current, next, t);
                case BoneJitterParameter.BlendState.Curve:
                    return (next - current) * (-2 * t + 3) * t * t + current;
                default:
                    return current;
            }
        }
    }
}