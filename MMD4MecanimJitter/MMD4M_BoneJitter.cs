using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MYB.MMD4MecanimJitter
{
    /// <summary>
    /// MMD4Mecanim BoneのuserRotation X,Y,Zを任意の波形で振幅させます。
    /// PlayOnce()を実行することで、Onceの波形を任意のタイミングでLoopの波形に加算出来ます。
    /// 同一Boneに対して複数のイベントを設定したい場合(首を仰ぐ、俯く等)、
    /// 対象とするBoneの階層下に複数アタッチすることで、親(isChild = false)が自動で設定されます。
    /// 親のUpdateにてuserRotationの更新が行われます。
    /// </summary>
    public class MMD4M_BoneJitter : BoneJitImpl
    {
        void OnEnable()
        {
            if (bone == null) return;

            PlayLoop();
        }

        void OnDisable()
        {
            if (bone == null) return;

            Initialize();
        }

        /// <summary>
        /// ループ再生開始
        /// </summary>
        public void PlayLoop()
        {
            StopLoop();
            
            if (!loopGroupEnabled) return;
            
            foreach (BoneJitterHelper h in helperList)
            {
                var routine = StartCoroutine(LoopCoroutine(h.loopState));
                loopRoutineList.Add(routine);
            }
        }
        
        /// <summary>
        /// ループ再生停止
        /// </summary>
        public void StopLoop()
        {
            ResetRoutineList(loopRoutineList);
            ResetAllLoopState();

            if (!isProcessing) ResetUserRotation();
        }

        /// <summary>
        /// 1周再生
        /// </summary>
        public void PlayOnce()
        {
            if (!onceGroupEnabled) return;

            StopOnce();
            
            foreach (BoneJitterHelper h in helperList)
            {
                //再生終了時にループ再生していない場合、初期化
                var routine = StartCoroutine(OnceCoroutine(h.onceState));
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

            if (!isProcessing) ResetUserRotation();
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

            ResetUserRotation();
        }

#if UNITY_EDITOR
        //Inspector拡張クラス
        [CanEditMultipleObjects]
        [CustomEditor(typeof(MMD4M_BoneJitter))]
        public class MMD4M_BoneJitterEditor : Editor
        {
            SerializedProperty syncAxisProperty;
            SerializedProperty angleMagnificationProperty;
            SerializedProperty maxDegreesDeltaProperty;
            SerializedProperty loopParameterProperty;
            SerializedProperty onceParameterProperty;

            void OnEnable()
            {
                var self = target as MMD4M_BoneJitter;
                
                syncAxisProperty = serializedObject.FindProperty("syncAxis");
                angleMagnificationProperty = serializedObject.FindProperty("angleMagnification");
                maxDegreesDeltaProperty = serializedObject.FindProperty("maxDegreesDelta");
                loopParameterProperty = serializedObject.FindProperty("loopParameter");
                onceParameterProperty = serializedObject.FindProperty("onceParameter");
            }

            bool childrenFolding = true;

            public override void OnInspectorGUI()
            {
                var self = target as MMD4M_BoneJitter;

                serializedObject.Update();

                self.bone = (MMD4MecanimBone)EditorGUILayout.ObjectField("MMD4M_Bone", self.bone, typeof(MMD4MecanimBone), true);
                if (self.bone == null) return;

                //children
                if (EditorApplication.isPlaying && !self.isChild)
                {
                    List<MMD4M_BoneJitter> list = self.children;
                    EditorGUI.BeginDisabledGroup(true);
                    if (childrenFolding = EditorGUILayout.Foldout(childrenFolding, "BoneJitter Children " + list.Count))
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            self.children[i] = (MMD4M_BoneJitter)EditorGUILayout.ObjectField(self.children[i], typeof(MMD4M_BoneJitter), true);
                        }
                    }
                    EditorGUI.EndDisabledGroup();
                }

                //isChild
                EditorGUI.BeginDisabledGroup(true);
                {
                    if (self.isChild)
                        self.isChild = EditorGUILayout.Toggle("is Child", self.isChild);
                }
                EditorGUI.EndDisabledGroup();


                //sync Axis
                EditorGUI.BeginDisabledGroup(EditorApplication.isPlaying);
                {
                    EditorGUI.BeginChangeCheck();
                    {
                        syncAxisProperty.boolValue = EditorGUILayout.Toggle(syncAxisProperty.displayName, syncAxisProperty.boolValue);
                    }
                    if (EditorGUI.EndChangeCheck()) self.OnValidate();
                }
                EditorGUI.EndDisabledGroup();

                if (!self.isChild)
                {
                    //angle Parameter
                    angleMagnificationProperty.floatValue = EditorGUILayout.FloatField(angleMagnificationProperty.displayName, angleMagnificationProperty.floatValue);
                    var delta = EditorGUILayout.FloatField(maxDegreesDeltaProperty.displayName, maxDegreesDeltaProperty.floatValue);
                    maxDegreesDeltaProperty.floatValue = Mathf.Max(0, delta);
                }

                //JitterParameter (Loop)
                if (!self.isChild)
                {
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
                    {
                        EditorGUI.indentLevel++;
                        for (int i = 0; i < 3; i++)
                        {
                            EditorGUI.BeginChangeCheck();
                            {
                                self.loopEnabled[i] = EditorGUILayout.ToggleLeft(self.axisLabel[i], self.loopEnabled[i], EditorStyles.boldLabel);
                                EditorGUILayout.PropertyField(loopParameterProperty.GetArrayElementAtIndex(i));
                            }
                            if (EditorGUI.EndChangeCheck()) self.OnValidate();
                        }
                        EditorGUI.indentLevel--;
                    }
                }

                //JitterParameter (Once)
                EditorGUI.BeginChangeCheck();
                self.onceGroupEnabled = EditorGUILayout.ToggleLeft("--- ONCE ---", self.onceGroupEnabled, EditorStyles.boldLabel);
                if (EditorGUI.EndChangeCheck() && EditorApplication.isPlaying)
                {
                    if (!self.onceGroupEnabled)
                        self.StopOnce();
                }

                if (self.onceGroupEnabled)
                {
                    EditorGUI.indentLevel++;
                    for (int i = 0; i < 3; i++)
                    {
                        EditorGUI.BeginChangeCheck();
                        self.onceEnabled[i] = EditorGUILayout.ToggleLeft(self.axisLabel[i], self.onceEnabled[i], EditorStyles.boldLabel);
                        if (self.onceEnabled[i])
                        {
                            EditorGUILayout.PropertyField(onceParameterProperty.GetArrayElementAtIndex(i));
                        }
                        if (EditorGUI.EndChangeCheck()) self.OnValidate();
                    }
                    EditorGUI.indentLevel--;

                    //Play Once
                    EditorGUILayout.BeginHorizontal();
                    {
                        GUILayout.FlexibleSpace();

                        EditorGUI.BeginDisabledGroup(!EditorApplication.isPlaying);
                        {
                            if (GUILayout.Button("Play Once", GUILayout.Width(100))) self.PlayOnce();
                        }
                        EditorGUI.EndDisabledGroup();
                    }
                    EditorGUILayout.EndHorizontal();
                }
                serializedObject.ApplyModifiedProperties();
            }
        }
#endif
    }
}