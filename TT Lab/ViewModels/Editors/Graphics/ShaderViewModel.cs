using Caliburn.Micro;
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TT_Lab.AssetData.Graphics;
using TT_Lab.AssetData.Graphics.Shaders;
using TT_Lab.Assets;
using TT_Lab.Attributes;
using TT_Lab.Rendering;
using TT_Lab.Rendering.Objects;
using TT_Lab.Util;
using TT_Lab.ViewModels.Composite;
using TT_Lab.ViewModels.Interfaces;
using TT_Lab.ViewModels.ResourceTree;
using Twinsanity.TwinsanityInterchange.Common;
using static Twinsanity.TwinsanityInterchange.Common.TwinShader;
using Vector4 = Twinsanity.TwinsanityInterchange.Common.Vector4;

namespace TT_Lab.ViewModels.Editors.Graphics
{
    public class ShaderViewModel : Conductor<IScreen>.Collection.AllActive, IHaveParentEditor<MaterialViewModel>, ISaveableViewModel<LabShader>, IHaveChildrenEditors
    {
        private String _name = "Shader";
        private TwinShader.Type _type;
        private UInt32 _intParam;
        private Single[] _floatParam;
        private AlphaBlending _alphaBlending;
        private AlphaBlendPresets _alphaRegSettingsIndex;
        private AlphaTest _alphaTest;
        private AlphaTestMethod _alphaTestMethod;
        private Byte _alphaValueToCompareTo;
        private ProcessAfterAlphaTestFailed _processAfterATestFailed;
        private DestinationAlphaTest _dAlphaTest;
        private DestinationAlphaTestMode _dAlphaTestMode;
        private DepthTestMethod _depthTest;
        private ShadingMethod _shdMethod;
        private TextureMapping _txtMapping;
        private TextureCoordinatesSpecification _texCoordSpec;
        private Fogging _fog;
        private Context _cxtNum;
        private Boolean _useCustomAlphaRegSettings;
        private ColorSpecMethod _specOfColA;
        private ColorSpecMethod _specOfColB;
        private AlphaSpecMethod _specOfAlphaC;
        private ColorSpecMethod _specOfColD;
        private Byte _fixedAlphaValue;
        private TextureFilter _texFilterWhenTextureIsExpanded;
        private Boolean _alphaCorrectionValue;
        private ZValueDrawMask _zValueDrawMask;
        private UInt16 _lodParamK;
        private UInt16 _lodParamL;
        private LabURI _texID;
        private Byte _unkVal1;
        private XScrollFormula _xScrollSettings;
        private YScrollFormula _yScrollSettings;
        private Boolean _unkFlag1;
        private Boolean _unkFlag2;
        private Boolean _unkFlag3;
        private Vector4ViewModel _unkVec1;
        private Vector4ViewModel _unkVec2;
        private Vector4ViewModel _uvScrollSpeed;
        private bool isDirty;
        private DirtyTracker dirtyTracker;

        public ShaderViewModel(MaterialViewModel materialEditor)
        {
            dirtyTracker = new DirtyTracker(this);
            ParentEditor = materialEditor;
            
            Type = TwinShader.Type.StandardUnlit;
            _name = Type.ToString();
            _floatParam = new Single[4];
            _unkVec1 = new Vector4ViewModel();
            _unkVec2 = new Vector4ViewModel();
            _uvScrollSpeed = new Vector4ViewModel();
            dirtyTracker.AddChild(_unkVec1);
            dirtyTracker.AddChild(_unkVec2);
            dirtyTracker.AddChild(_uvScrollSpeed);
            _texID = LabURI.Empty;
        }

        public ShaderViewModel(LabShader shader, MaterialViewModel materialEditor) : this(materialEditor)
        {
            Type = shader.ShaderType;
            _intParam = shader.IntParam;
            for (var i = 0; i < _floatParam.Length; ++i)
            {
                _floatParam[i] = shader.FloatParam[i];
            }
            _alphaBlending = shader.ABlending;
            _alphaRegSettingsIndex = shader.AlphaRegSettingsIndex;
            _alphaTest = shader.ATest;
            _alphaTestMethod = shader.ATestMethod;
            _alphaValueToCompareTo = shader.AlphaValueToBeComparedTo;
            _processAfterATestFailed = shader.ProcessMethodWhenAlphaTestFailed;
            _dAlphaTest = shader.DAlphaTest;
            _dAlphaTestMode = shader.DAlphaTestMode;
            _depthTest = shader.DepthTest;
            _shdMethod = shader.ShdMethod;
            _txtMapping = shader.TxtMapping;
            _texCoordSpec = shader.MethodOfSpecifyingTextureCoordinates;
            _fog = shader.Fog;
            _cxtNum = shader.ContextNum;
            _useCustomAlphaRegSettings = shader.UseCustomAlphaRegSettings;
            _specOfColA = shader.SpecOfColA;
            _specOfColB = shader.SpecOfColB;
            _specOfAlphaC = shader.SpecOfAlphaC;
            _specOfColD = shader.SpecOfColD;
            _fixedAlphaValue = shader.FixedAlphaValue;
            _texFilterWhenTextureIsExpanded = shader.TextureFilterWhenTextureIsExpanded;
            _alphaCorrectionValue = shader.AlphaCorrectionValue;
            _zValueDrawMask = shader.ZValueDrawingMask;
            _lodParamK = shader.LodParamK;
            _lodParamL = shader.LodParamL;
            _texID = shader.TextureId;
            _unkVal1 = shader.UnkVal1;
            _xScrollSettings = shader.XScrollSettings;
            _yScrollSettings = shader.YScrollSettings;
            _unkFlag1 = shader.UnkFlag1;
            _unkFlag2 = shader.UnkFlag2;
            _unkFlag3 = shader.UnkFlag3;
            dirtyTracker.RemoveChild(_unkVec1);
            dirtyTracker.RemoveChild(_unkVec2);
            dirtyTracker.RemoveChild(_uvScrollSpeed);
            _unkVec1 = new Vector4ViewModel(shader.UnkVector1);
            _unkVec2 = new Vector4ViewModel(shader.UnkVector2);
            _uvScrollSpeed = new Vector4ViewModel(shader.UvScrollSpeed);
            dirtyTracker.AddChild(_unkVec1);
            dirtyTracker.AddChild(_unkVec2);
            dirtyTracker.AddChild(_uvScrollSpeed);
        }

        public void Save(LabShader o)
        {
            Copy(o);
            ResetDirty();
        }
        
        
        public void Copy(LabShader o)
        {
            o.ShaderType = Type;
            o.IntParam = IntParam;
            o.FloatParam = new Single[4];
            for (var j = 0; j < 4; ++j)
            {
                o.FloatParam[j] = FloatParam[j];
            }
            o.ABlending = AlphaBlending ? TwinShader.AlphaBlending.ON : TwinShader.AlphaBlending.OFF;
            o.AlphaRegSettingsIndex = AlphaRegSettingsIndex;
            o.ATest = AlphaTest ? TwinShader.AlphaTest.ON : TwinShader.AlphaTest.OFF;
            o.ATestMethod = AlphaTestMethod;
            o.AlphaValueToBeComparedTo = AlphaValueToCompareTo;
            o.ProcessMethodWhenAlphaTestFailed = ProcessAfterATestFailed;
            o.DAlphaTest = DAlphaTest ? DestinationAlphaTest.ON : DestinationAlphaTest.OFF;
            o.DAlphaTestMode = DAlphaTestMode;
            o.DepthTest = DepthTest;
            o.ShdMethod = ShdMethod;
            o.TxtMapping = TxtMapping ? TextureMapping.ON : TextureMapping.OFF;
            o.MethodOfSpecifyingTextureCoordinates = TexCoordSpec;
            o.Fog = Fog ? Fogging.ON : Fogging.OFF;
            o.ContextNum = CxtNum;
            o.UseCustomAlphaRegSettings = UseCustomAlphaRegSettings;
            o.SpecOfColA = SpecOfColA;
            o.SpecOfColB = SpecOfColB;
            o.SpecOfAlphaC = SpecOfAlphaC;
            o.SpecOfColD = SpecOfColD;
            o.FixedAlphaValue = FixedAlphaValue;
            o.TextureFilterWhenTextureIsExpanded = TexFilterWhenTextureIsExpanded;
            o.AlphaCorrectionValue = AlphaCorrectionValue;
            o.ZValueDrawingMask = ZValueDrawMask;
            o.LodParamK = LodParamK;
            o.LodParamL = LodParamL;
            o.TextureId = TexID;
            o.UnkVal1 = UnkVal1;
            o.XScrollSettings = XScrollSettings;
            o.YScrollSettings = YScrollSettings;
            o.UnkFlag1 = UnkFlag1;
            o.UnkFlag2 = UnkFlag2;
            o.UnkFlag3 = UnkFlag3;
            o.UnkVector1 = new Vector4
            {
                X = UnkVec1.X,
                Y = UnkVec1.Y,
                Z = UnkVec1.Z,
                W = UnkVec1.W
            };
            o.UnkVector2 = new Vector4
            {
                X = UnkVec2.X,
                Y = UnkVec2.Y,
                Z = UnkVec2.Z,
                W = UnkVec2.W,
            };
            o.UvScrollSpeed = new Vector4
            {
                X = UvScrollSpeed.X,
                Y = UvScrollSpeed.Y,
                Z = UvScrollSpeed.Z,
                W = UvScrollSpeed.W,
            };
        }

        public MaterialViewModel ParentEditor { get; set; }

        [MarkDirty]
        public String Name
        {
            get => _name;
            private set
            {
                if (value != _name)
                {
                    _name = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        public string TexturePath => TexID == LabURI.Empty ? ManifestResourceLoader.GetPathInExe("Media\\boat_guy.png") : AssetManager.Get().GetAsset(TexID).FullDataPath;

        public Boolean HasIntParam => _type is TwinShader.Type.UnlitClothDeformation or TwinShader.Type.UnlitClothDeformation2;

        public Boolean HasFloatParam1
        {
            get
            {
                return _type switch
                {
                    TwinShader.Type.LitReflectionSurface or TwinShader.Type.SHADER_17 or TwinShader.Type.UnlitClothDeformation or TwinShader.Type.UnlitClothDeformation2 => true,
                    _ => false,
                };
            }
        }

        public Boolean HasFloatParam2
        {
            get
            {
                return _type switch
                {
                    TwinShader.Type.UnlitClothDeformation or TwinShader.Type.UnlitClothDeformation2 => true,
                    _ => false,
                };
            }
        }

        public Boolean HasFloatParam3
        {
            get
            {
                return _type switch
                {
                    TwinShader.Type.UnlitClothDeformation2 => true,
                    _ => false,
                };
            }
        }

        public Boolean HasFloatParam4
        {
            get
            {
                return _type switch
                {
                    TwinShader.Type.UnlitClothDeformation2 => true,
                    _ => false,
                };
            }
        }

        public Boolean CanColorSpec
        {
            get => UseCustomAlphaRegSettings && AlphaBlending;
        }

        public Boolean UsePresetBlending
        {
            get => !UseCustomAlphaRegSettings && AlphaBlending;
        }

        [MarkDirty]
        public TwinShader.Type Type
        {
            get => _type;
            set
            {
                if (value != _type)
                {
                    _type = value;
                    _name = $"{_type}";
                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(HasIntParam));
                    NotifyOfPropertyChange(nameof(HasFloatParam1));
                    NotifyOfPropertyChange(nameof(HasFloatParam2));
                    NotifyOfPropertyChange(nameof(HasFloatParam3));
                    NotifyOfPropertyChange(nameof(HasFloatParam4));
                    NotifyOfPropertyChange(nameof(Name));
                }
            }
        }
        [MarkDirty]
        public UInt32 IntParam
        {
            get => _intParam;
            set
            {
                if (value != _intParam)
                {
                    _intParam = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        [MarkDirty]
        public Single[] FloatParam { get => _floatParam; private set => _floatParam = value; }
        
        [MarkDirty]
        public Boolean AlphaBlending
        {
            get => _alphaBlending == TwinShader.AlphaBlending.ON;
            set
            {
                if (AlphaBlending != value)
                {
                    _alphaBlending = value ? TwinShader.AlphaBlending.ON : TwinShader.AlphaBlending.OFF;
                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(CanColorSpec));
                    NotifyOfPropertyChange(nameof(UsePresetBlending));
                }
            }
        }
        [MarkDirty]
        public AlphaBlendPresets AlphaRegSettingsIndex
        {
            get => _alphaRegSettingsIndex;
            set
            {
                if (_alphaRegSettingsIndex != value)
                {
                    _alphaRegSettingsIndex = value;
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public Boolean AlphaTest
        {
            get => _alphaTest == TwinShader.AlphaTest.ON;
            set
            {
                if (AlphaTest != value)
                {
                    _alphaTest = value ? TwinShader.AlphaTest.ON : TwinShader.AlphaTest.OFF;
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public AlphaTestMethod AlphaTestMethod
        {
            get => _alphaTestMethod;
            set
            {
                if (_alphaTestMethod != value)
                {
                    _alphaTestMethod = value;
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public Byte AlphaValueToCompareTo
        {
            get => _alphaValueToCompareTo;
            set
            {
                if (_alphaValueToCompareTo != value)
                {
                    _alphaValueToCompareTo = value;
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public ProcessAfterAlphaTestFailed ProcessAfterATestFailed
        {
            get => _processAfterATestFailed;
            set
            {
                if (_processAfterATestFailed != value)
                {
                    _processAfterATestFailed = value;
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public Boolean DAlphaTest
        {
            get => _dAlphaTest == DestinationAlphaTest.ON;
            set
            {
                if (DAlphaTest != value)
                {
                    _dAlphaTest = value ? DestinationAlphaTest.ON : DestinationAlphaTest.OFF;
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public DestinationAlphaTestMode DAlphaTestMode
        {
            get => _dAlphaTestMode;
            set
            {
                if (_dAlphaTestMode != value)
                {
                    _dAlphaTestMode = value;
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public DepthTestMethod DepthTest
        {
            get => _depthTest;
            set
            {
                if (_depthTest != value)
                {
                    _depthTest = value;
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public ShadingMethod ShdMethod
        {
            get => _shdMethod;
            set
            {
                if (_shdMethod != value)
                {
                    _shdMethod = value;
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public Boolean TxtMapping
        {
            get => _txtMapping == TextureMapping.ON;
            set
            {
                if (TxtMapping != value)
                {
                    _txtMapping = value ? TextureMapping.ON : TextureMapping.OFF;
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public TextureCoordinatesSpecification TexCoordSpec
        {
            get => _texCoordSpec;
            set
            {
                if (_texCoordSpec != value)
                {
                    _texCoordSpec = value;
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public Boolean Fog
        {
            get => _fog == Fogging.ON;
            set
            {
                if (Fog != value)
                {
                    _fog = value ? Fogging.ON : Fogging.OFF;
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public Context CxtNum
        {
            get => _cxtNum;
            set
            {
                if (_cxtNum != value)
                {
                    _cxtNum = value;
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public Boolean UseCustomAlphaRegSettings
        {
            get => _useCustomAlphaRegSettings;
            set
            {
                if (_useCustomAlphaRegSettings != value)
                {
                    _useCustomAlphaRegSettings = value;
                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(CanColorSpec));
                    NotifyOfPropertyChange(nameof(UsePresetBlending));
                }
            }
        }
        [MarkDirty]
        public ColorSpecMethod SpecOfColA
        {
            get => _specOfColA;
            set
            {
                if (_specOfColA != value)
                {
                    _specOfColA = value;
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public ColorSpecMethod SpecOfColB
        {
            get => _specOfColB;
            set
            {
                if (_specOfColB != value)
                {
                    _specOfColB = value;
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public AlphaSpecMethod SpecOfAlphaC
        {
            get => _specOfAlphaC;
            set
            {
                if (_specOfAlphaC != value)
                {
                    _specOfAlphaC = value;
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public ColorSpecMethod SpecOfColD
        {
            get => _specOfColD;
            set
            {
                if (_specOfColD != value)
                {
                    _specOfColD = value;
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public Byte FixedAlphaValue
        {
            get => _fixedAlphaValue;
            set
            {
                if (_fixedAlphaValue != value)
                {
                    _fixedAlphaValue = value;
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public TextureFilter TexFilterWhenTextureIsExpanded
        {
            get => _texFilterWhenTextureIsExpanded;
            set
            {
                if (_texFilterWhenTextureIsExpanded != value)
                {
                    _texFilterWhenTextureIsExpanded = value;
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public Boolean AlphaCorrectionValue
        {
            get => _alphaCorrectionValue;
            set
            {
                if (_alphaCorrectionValue != value)
                {
                    _alphaCorrectionValue = value;
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public ZValueDrawMask ZValueDrawMask
        {
            get => _zValueDrawMask;
            set
            {
                if (_zValueDrawMask != value)
                {
                    _zValueDrawMask = value;
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public UInt16 LodParamK
        {
            get => _lodParamK;
            set
            {
                if (_lodParamK != value)
                {
                    _lodParamK = value;
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public UInt16 LodParamL
        {
            get => _lodParamL;
            set
            {
                if (_lodParamL != value)
                {
                    _lodParamL = value;
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public LabURI TexID
        {
            get => _texID;
            set
            {
                if (_texID != value)
                {
                    _texID = value;
                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(TexturePath));
                }
            }
        }
        [MarkDirty]
        public Byte UnkVal1
        {
            get => _unkVal1;
            set
            {
                if (_unkVal1 != value)
                {
                    _unkVal1 = value;
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public XScrollFormula XScrollSettings
        {
            get => _xScrollSettings;
            set
            {
                if (_xScrollSettings != value)
                {
                    _xScrollSettings = value;
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public YScrollFormula YScrollSettings
        {
            get => _yScrollSettings;
            set
            {
                if (_yScrollSettings != value)
                {
                    _yScrollSettings = value;
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public Boolean UnkFlag1
        {
            get => _unkFlag1;
            set
            {
                if (_unkFlag1 != value)
                {
                    _unkFlag1 = value;
                    NotifyOfPropertyChange();
                }
            }
        }
        [MarkDirty]
        public Boolean UnkFlag2
        {
            get => _unkFlag2;
            set
            {
                if (_unkFlag2 != value)
                {
                    _unkFlag2 = value;
                    NotifyOfPropertyChange();
                }
            }
        }
        
        [MarkDirty]
        public Boolean UnkFlag3
        {
            get => _unkFlag3;
            set
            {
                if (_unkFlag3 != value)
                {
                    _unkFlag3 = value;
                    NotifyOfPropertyChange();
                }
            }
        }
        
        public Vector4ViewModel UnkVec1 => _unkVec1;

        public Vector4ViewModel UnkVec2 => _unkVec2;

        public Vector4ViewModel UvScrollSpeed => _uvScrollSpeed;

        public void ResetDirty()
        {
            dirtyTracker.ResetDirty();
        }

        public bool IsDirty => dirtyTracker.IsDirty;

        public DirtyTracker DirtyTracker => dirtyTracker;
    }
}
