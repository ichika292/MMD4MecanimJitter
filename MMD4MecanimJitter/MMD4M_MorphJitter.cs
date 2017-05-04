using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif

namespace MYB.MMD4MecanimJitter
{
    /// <summary>
    /// MMD4Mecanim ModelのmorphWeightを任意の波形で振幅させます。
    /// PlayOnce()を実行することで、Onceの波形を任意のタイミングでLoopの波形に加算出来ます。
    /// </summary>
    public class MMD4M_MorphJitter : MorphJitImpl
    {
        void OnEnable()
        {
            if (_model == null) return;

            PlayLoop();
        }

        void OnDisable()
        {
            if (_model == null) return;

            Initialize();
        }

        /// <summary>
        /// ループ再生開始
        /// </summary>
        public void PlayLoop()
        {
            if (helperList.Count == 0) return;

            StopLoop();

            if (!loopGroupEnabled) return;

            if (sync)
            {
                //helperList[0]のstateを全モーフで共有
                var routine = StartCoroutine(LoopCoroutine(helperList[0].loopState));
                loopRoutineList.Add(routine);
            }
            else
            {
                foreach (MorphJitterHelper h in helperList)
                {
                    var routine = StartCoroutine(LoopCoroutine(h.loopState));
                    loopRoutineList.Add(routine);
                }
            }
        }

        /// <summary>
        /// ループ再生停止
        /// </summary>
        public void StopLoop()
        {
            ResetRoutineList(loopRoutineList);
            ResetAllLoopState();

            SetMorphWeight();
        }

        /// <summary>
        /// 1周再生
        /// </summary>
        public void PlayOnce()
        {
            if (!onceGroupEnabled) return;

            StopOnce();

            foreach (MorphJitterHelper h in helperList)
            {
                //再生終了時にループ再生していない場合、初期化
                var routine = StartCoroutine(OnceCoroutine(h.onceState, StopOnce));
                onceRoutineList.Add(routine);
            }
        }

        /// <summary>
        /// 1周再生停止
        /// </summary>
        public void StopOnce()
        {
            ResetRoutineList(onceRoutineList);
            ResetAllOnceState();

            SetMorphWeight();
        }

        /// <summary>
        /// 全再生停止 & 初期化
        /// </summary>
        public void Initialize()
        {
            ResetRoutineList(loopRoutineList);
            ResetAllLoopState();

            ResetRoutineList(onceRoutineList);
            ResetAllOnceState();

            SetMorphWeight();
        }

#if UNITY_EDITOR
        //Inspector拡張クラス
        [CanEditMultipleObjects]
        [CustomEditor(typeof(MMD4M_MorphJitter))]
        public class MMD4M_MorphJitterEditor : Editor
        {
            SerializedProperty syncProperty;
            SerializedProperty helperListProperty;
            SerializedProperty loopParameterProperty;
            SerializedProperty onceParameterProperty;

            ReorderableList helperReorderableList;

            void OnEnable()
            {
                var self = target as MMD4M_MorphJitter;
                
                syncProperty = serializedObject.FindProperty("sync");
                helperListProperty = serializedObject.FindProperty("helperList");
                loopParameterProperty = serializedObject.FindProperty("loopParameter");
                onceParameterProperty = serializedObject.FindProperty("onceParameter");

                //ReorderableList設定
                helperReorderableList = new ReorderableList(serializedObject, helperListProperty);

                helperReorderableList.drawHeaderCallback = (rect) =>
                {
                    EditorGUI.LabelField(rect, "Morph name & Magnification");
                };

                helperReorderableList.drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    var element = helperListProperty.GetArrayElementAtIndex(index);
                    rect.height -= 4;
                    rect.y += 2;
                    EditorGUI.PropertyField(rect, element);
                };

                //再生中はListの追加、削除禁止
                helperReorderableList.onAddCallback = (list) =>
                {
                    if (!EditorApplication.isPlaying)
                    {
                        self.helperList.Add(new MorphJitterHelper(self, "", 1f));
                        list.index = helperListProperty.arraySize;
                    }
                };

                helperReorderableList.onCanRemoveCallback = (list) =>
                {
                    return !EditorApplication.isPlaying;
                };
            }

            public override void OnInspectorGUI()
            {
                var self = target as MMD4M_MorphJitter;

                serializedObject.Update();

                self._model = (MMD4MecanimModel)EditorGUILayout.ObjectField("MMD4M_Model", self._model, typeof(MMD4MecanimModel), true);

                if (self._model == null) return;
                
                //sync
                EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
                syncProperty.boolValue = EditorGUILayout.Toggle(syncProperty.displayName, syncProperty.boolValue);
                EditorGUI.EndDisabledGroup();

                //JitterParameter (Loop)
                EditorGUI.BeginChangeCheck();
                self.loopGroupEnabled = EditorGUILayout.ToggleLeft("--- LOOP ---", self.loopGroupEnabled, EditorStyles.boldLabel);
                if (EditorGUI.EndChangeCheck() && EditorApplication.isPlaying)
                {
                    if (self.loopGroupEnabled)
                        self.PlayLoop();
                    else
                        self.StopLoop();
                } 

                if (self.loopGroupEnabled)
                    EditorGUILayout.PropertyField(loopParameterProperty);

                //JitterParameter (Once)
                EditorGUI.BeginChangeCheck();
                self.onceGroupEnabled = EditorGUILayout.ToggleLeft("--- ONCE ---", self.onceGroupEnabled, EditorStyles.boldLabel);
                if (EditorGUI.EndChangeCheck() && EditorApplication.isPlaying)
                {
                    if (!self.onceGroupEnabled)
                        self.StopOnce();
                }

                if (self.onceGroupEnabled)
                    EditorGUILayout.PropertyField(onceParameterProperty);

                //Helper List
                helperReorderableList.DoLayoutList();

                //MecanimModelからWeight>0のモーフを取得
                EditorGUILayout.BeginHorizontal();
                {
                    EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
                    if (GUILayout.Button("Set", GUILayout.Width(80))) self.SetMorph();
                    if (GUILayout.Button("Reset", GUILayout.Width(80))) self.ResetMorph();
                    EditorGUI.EndDisabledGroup();

                    GUILayout.FlexibleSpace();

                    EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying | !self.onceGroupEnabled);
                    if (GUILayout.Button("Play Once", GUILayout.Width(100))) self.PlayOnce();
                    EditorGUI.EndDisabledGroup();
                }
                EditorGUILayout.EndHorizontal();

                serializedObject.ApplyModifiedProperties();
            }
        }
#endif
    }
}