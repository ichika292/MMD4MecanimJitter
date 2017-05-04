# MMD4MecanimJitter
UnityのMMD4Mecanim用アドオンです。  
morphWeightやBoneをプロージャル生成した波形で振幅させます。ループ再生しつつ、任意のタイミングで別の波形を加算することが出来ます。  
詳細な解説は[こちら](http://ichika292.hatenablog.com/entry/2017/05/04/212540)
## MMD4M_MorphJitter
MMD4MecanimModelImpl.Morph.weightに計算結果を出力して振幅させます。
## MMD4M_BoneJitter
MecanimBoneのuserRotationに計算結果を出力して振幅させます。

userRotationはidentity以外のときにAnimatorを上書きする仕様のようで、併用するとidentity付近で不連続な挙動を起こす恐れがある。  
**TODO:** userRotationを介さない回転の乗算→Animatorとのブレンドが可能に？
## MMD4M_EyeJitter
MecanimBoneのuserRotationを使ったサッケード眼球運動。
