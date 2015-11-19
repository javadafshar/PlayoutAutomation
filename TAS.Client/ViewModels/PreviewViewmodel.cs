﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.ObjectModel;
using TAS.Common;
using TAS.Client.Common;
using TAS.Server.Interfaces;

namespace TAS.Client.ViewModels
{
    public class PreviewViewmodel : ViewmodelBase
    {
        private IMedia _media;
        private IEvent _event;
        private readonly IEngine _engine;
        private readonly IPlayoutServerChannel _channelPRV;
        private readonly IMediaManager _mediaManager;
        private static readonly int _sliderMaximum = 100;
        public PreviewViewmodel(IEngine engine)
        {
            engine.PropertyChanged += this.EnginePropertyChanged;
            engine.ServerPropertyChanged += this.OnServerPropertyChanged;
            _channelPRV = engine.PlayoutChannelPRV;
            _engine = engine;
            _mediaManager = engine.MediaManager;
            CreateCommands();
        }

        protected override void OnDispose()
        {
            _engine.PropertyChanged -= this.EnginePropertyChanged;
            _engine.ServerPropertyChanged -= this.OnServerPropertyChanged;
            SelectedSegment = null;
        }

        public RationalNumber FrameRate { get { return _engine.FormatDescription.FrameRate; } }

        public IMedia Media
        {
            get { return _media; }
            set
            {
                if (_channelPRV != null)
                {
                    IMedia newVal = _mediaManager.GetPRVMedia(value);
                    if (SetField(ref _media, newVal, "Media"))
                    {
                        _event = null;
                        NotifyPropertyChanged("Event");
                        NotifyPropertyChanged("CommandPause");
                        NotifyPropertyChanged("CommandPlay");
                    }
                }
            }
        }

        public IEvent Event
        {
            get { return _event; }
            set
            {
                if (SetField(ref _event, value, "Event"))
                {
                    _media = null;
                    NotifyPropertyChanged("Media");
                    NotifyPropertyChanged("CommandPause");
                    NotifyPropertyChanged("CommandPlay");
                }
            }
        }

        private IServerMedia _loadedMedia;
        public IServerMedia LoadedMedia
        {
            get { return _loadedMedia; }
            set { SetField(ref _loadedMedia, value, "LoadedMedia"); }
        }

        private long _loadedSeek;
        private long _loadedDuration;

        private void MediaLoad(bool reloadSegments)
        {
            IMedia pm = Media;
            IServerMedia media = null;
            TimeSpan tcIn = TimeSpan.Zero;
            TimeSpan duration = TimeSpan.Zero;
            MediaSegmentViewmodel segment = reloadSegments ? null : SelectedSegment;
            if (reloadSegments)
                PlayWholeClip = false;
            if (pm is IServerMedia)
            {
                media = (IServerMedia)pm;
                tcIn = _playWholeClip ? media.TCStart : (segment == null)? media.TCPlay: segment.TCIn;
                duration = _playWholeClip ? media.Duration : (segment == null)? media.DurationPlay: segment.Duration;
            }
            IEvent ev = Event;
            if (ev != null)
            {
                media = ev.ServerMediaPRV;
                if (media != null)
                {
                    tcIn = _playWholeClip ? media.TCStart : (segment == null)? ev.ScheduledTC: segment.TCIn;
                    duration = _playWholeClip ? media.Duration : (segment == null)? ev.Duration: segment.Duration;
                }
            }

            if (media != null
                && duration.Ticks >= _engine.FrameTicks)
            {
                LoadedMedia = media;
                TCIn = tcIn;
                TCOut = tcIn + duration - TimeSpan.FromTicks(_engine.FrameTicks);
                if (reloadSegments)
                {
                    MediaSegments.Clear();
                    foreach (IMediaSegment ms in media.MediaSegments.ToList())
                        MediaSegments.Add(new MediaSegmentViewmodel(media, ms));
                }
                _loadedSeek = (tcIn.Ticks - media.TCStart.Ticks) / _engine.FrameTicks;
                long newPosition = _engine.PreviewLoaded ? _engine.PreviewSeek + _engine.PreviewPosition - _loadedSeek : 0;
                if (newPosition < 0)
                    newPosition = 0;
                _loadedDuration = duration.Ticks / _engine.FrameTicks;
                _engine.PreviewLoad(media, _loadedSeek, _loadedDuration, newPosition);
            }
            NotifyPropertyChanged(null);
        }

        void _mediaUnload()
        {
            TCIn = TimeSpan.Zero;
            TCOut = TimeSpan.Zero;
            LoadedMedia = null;
            _engine.PreviewUnload();
            NotifyPropertyChanged(null);
        }

        public TimeSpan StartTC
        {
            get { return (LoadedMedia == null) ? TimeSpan.Zero : LoadedMedia.TCStart; }
        }

        private TimeSpan _tCIn;
        public TimeSpan TCIn
        {
            get { return _tCIn; }
            set
            {
                if (SetField(ref _tCIn, value, "TCIn"))
                    NotifyPropertyChanged("CommandSaveSegment");
            }
        }

        private TimeSpan _tCOut;
        public TimeSpan TCOut
        {
            get { return _tCOut; }
            set
            {
                if (SetField(ref _tCOut, value, "TCOut"))
                    NotifyPropertyChanged("CommandSaveSegment");
            }
        }

        public TimeSpan DurationSelection { get { return new TimeSpan(TCOut.Ticks - TCIn.Ticks + _engine.FrameTicks); } }

        public TimeSpan Position
        {
            get
            {
                return TimeSpan.FromTicks((long)((_engine.PreviewPosition + _engine.PreviewSeek) * TimeSpan.TicksPerSecond * FrameRate.Den / FrameRate.Num) + StartTC.Ticks);
            }
            set
            {
                _engine.PreviewPosition = (value.Ticks - StartTC.Ticks) / _engine.FrameTicks - _loadedSeek;
                NotifyPropertyChanged("SliderPosition");
            }
        }

        public bool IsPlayable { get { return LoadedMedia != null && LoadedMedia.MediaStatus == TMediaStatus.Available; } }
        public bool IsLoaded { get { return LoadedMedia != null; } }

        public string MediaName
        {
            get
            {
                IServerMedia loadedMedia = LoadedMedia;
                return (loadedMedia == null) ? string.Empty : loadedMedia.MediaName;
            }
        }
        public string FileName 
        {
            get
            {
                IServerMedia loadedMedia = LoadedMedia;
                return (loadedMedia == null) ? string.Empty : loadedMedia.FileName;
            }
        }
        
        public long SliderPosition
        {
            get
            {
                return _loadedDuration < 1 ? 0 : (_engine.PreviewPosition * _sliderMaximum) / (_loadedDuration-1);
            }
            set 
            {
                long newPos = _loadedSeek + (value * (_loadedDuration -1)) / _sliderMaximum;
                Position = TimeSpan.FromTicks(newPos * _engine.FrameTicks + StartTC.Ticks);
                NotifyPropertyChanged("Position");
            }
        }

        public void OnServerPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_channelPRV != null
                && sender == _channelPRV.OwnerServer)
                NotifyPropertyChanged("IsEnabled");
        }
        

        private ObservableCollection<MediaSegmentViewmodel> _mediaSegments = new ObservableCollection<MediaSegmentViewmodel>();
        public ObservableCollection<MediaSegmentViewmodel> MediaSegments { get { return _mediaSegments; } }

        private MediaSegmentViewmodel _selectedSegment;
        public MediaSegmentViewmodel SelectedSegment
        {
            get { return _selectedSegment; }
            set
            {
                MediaSegmentViewmodel oldValue = _selectedSegment;
                if (oldValue != value)
                {
                    if (oldValue != null)
                        oldValue.PropertyChanged -= SegmentPropertyChanged;
                    _selectedSegment = value;
                    if (value != null)
                    {
                        value.PropertyChanged += SegmentPropertyChanged;
                        SelectedSegmentName = value.SegmentName;
                    }
                    else
                        SelectedSegmentName = string.Empty;
                    NotifyPropertyChanged("CommandSaveSegment");
                    NotifyPropertyChanged("CommandDeleteSegment");
                    NotifyPropertyChanged("SelectedSegmentName");
                    NotifyPropertyChanged("SelectedSegment");
                    MediaLoad(false);
                }
            }
        }


        private string _selectedSegmentName;
        public string SelectedSegmentName
        {
            get { return _selectedSegmentName; }
            set
            {
                if (_selectedSegmentName != value)
                {
                    _selectedSegmentName = value;
                    NotifyPropertyChanged("SelectedSegmentName");
                    NotifyPropertyChanged("CommandSaveSegment");
                }
            }
        }

        public bool IsEnabled
        {
            get
            {
                if (_channelPRV != null)
                {
                    var server = _channelPRV.OwnerServer;
                    if (server != null)
                        return server.IsConnected;
                }
                return false;
            }
        } 

        public bool IsSegmentNameFocused { get; set; }

        public TimeSpan Duration
        {
            get
            {
                IServerMedia loadedMedia = LoadedMedia;
                return (loadedMedia == null) ? TimeSpan.Zero : loadedMedia.Duration;
            }
        }

        protected TimeSpan MaxPos()
        {
            IServerMedia loadedMedia = LoadedMedia;
            if (loadedMedia != null)
            {
                if (_playWholeClip)
                    return loadedMedia.TCStart + loadedMedia.Duration;
                else
                    return TCOut;
            }
            return TimeSpan.Zero;
        }

        private bool _playWholeClip;
        public bool PlayWholeClip
        {
            get { return _playWholeClip; }
            set
            {
                if (SetField(ref _playWholeClip, value, "PlayWholeClip"))
                {
                    MediaLoad(false);
                    NotifyPropertyChanged("SliderPosition");
                }
            }
        }

        public UICommand CommandPause { get; private set; }
        public UICommand CommandPlay { get; private set; }
        public UICommand CommandStop { get; private set; }
        public UICommand CommandSeek { get; private set; }
        public UICommand CommandCopyToTCIn { get; private set; }
        public UICommand CommandCopyToTCOut { get; private set; }
        public UICommand CommandSaveSegment { get; private set; }
        public UICommand CommandDeleteSegment { get; private set; }
        public UICommand CommandNewSegment { get; private set; }
        public UICommand CommandSetSegmentNameFocus { get; private set; }

        private void CreateCommands()
        {
            CommandPause = new UICommand()
            {
                ExecuteDelegate = o =>
                    {
                        if (LoadedMedia == null)
                            MediaLoad(true);
                        else
                            if (_engine.PreviewIsPlaying)
                                _engine.PreviewPause();
                            else
                            {
                                _engine.PreviewPosition = 0;
                                NotifyPropertyChanged("Position");
                                NotifyPropertyChanged("SliderPosition");
                            }
                    },
                CanExecuteDelegate = o =>
                    {
                        IMedia media = Media ?? (Event != null ? Event.Media : null);
                        return (LoadedMedia != null && LoadedMedia.MediaStatus == TMediaStatus.Available)
                            || (media != null && media.MediaStatus == TMediaStatus.Available && _canLoad(media));
                    }
            };
            CommandPlay = new UICommand()
            {
                ExecuteDelegate = o =>
                    {
                        IServerMedia loadedMedia = LoadedMedia;
                        if (loadedMedia != null)
                        {
                            if (_engine.PreviewIsPlaying)
                                _engine.PreviewPause();
                            else
                                _engine.PreviewPlay();
                        }
                        else
                        {
                            CommandPause.Execute(null);
                            if (LoadedMedia != null)
                                _engine.PreviewPlay();
                        }
                    },
                CanExecuteDelegate = o =>
                    {
                        IMedia media = Media ?? (Event != null ? Event.Media : null);
                        return (LoadedMedia != null && LoadedMedia.MediaStatus == TMediaStatus.Available)
                            || (media != null && media.MediaStatus == TMediaStatus.Available && _canLoad(media));
                    }
            };
            CommandStop = new UICommand()
            {
                ExecuteDelegate = o => { _mediaUnload(); },
                CanExecuteDelegate = _canStop                   
            };
            CommandSeek = new UICommand()
            {
                ExecuteDelegate = param =>
                    {
                        if (_engine.PreviewIsPlaying)
                            _engine.PreviewPause();
                        int seekFrames = 0;
                        switch ((string)param)
                        {
                            case "fframe": seekFrames = 1;
                                break;
                            case "rframe": seekFrames = -1;
                                break;
                            case "fsecond": seekFrames = (int)(FrameRate.Num / FrameRate.Den);
                                break;
                            case "rsecond":
                                seekFrames = -(int)(FrameRate.Num / FrameRate.Den);
                                break;
                        }
                        _engine.PreviewPosition = _engine.PreviewPosition + seekFrames;
                        NotifyPropertyChanged("Position");
                        NotifyPropertyChanged("SliderPosition");
                    },
                CanExecuteDelegate = _canStop
            };

            CommandCopyToTCIn = new UICommand()
            {
                ExecuteDelegate = o =>
                    {
                        TCIn = Position;
                    },
                CanExecuteDelegate = _canStop
            };

            CommandCopyToTCOut = new UICommand()
            {
                ExecuteDelegate = o =>
                    {
                        TCOut = Position;
                    },
                CanExecuteDelegate = _canStop
            };

            CommandSaveSegment = new UICommand()
            {
                ExecuteDelegate = o =>
                    {
                        MediaSegmentViewmodel msVm = _selectedSegment;
                        IServerMedia media = LoadedMedia;
                        if (msVm == null)
                        {
                            msVm = new MediaSegmentViewmodel(media) { TCIn = this.TCIn, TCOut = this.TCOut, SegmentName = this.SelectedSegmentName };
                            media.MediaSegments.Add(msVm.MediaSegment);
                            MediaSegments.Add(msVm);
                            SelectedSegment = msVm;
                        }
                        else
                        {
                            msVm.TCIn = TCIn;
                            msVm.TCOut = TCOut;
                            msVm.SegmentName = SelectedSegmentName;
                        }
                        msVm.Save();
                    },
                CanExecuteDelegate = o =>
                    {
                        var ss = SelectedSegment;
                        return (LoadedMedia != null 
                            && ((ss == null && !string.IsNullOrEmpty(SelectedSegmentName))
                                || (ss != null && (ss.Modified || SelectedSegmentName != ss.SegmentName || TCIn != ss.TCIn || TCOut != ss.TCOut))));
                    }
            };
            CommandDeleteSegment = new UICommand()
            {
                ExecuteDelegate = o =>
                    {
                        MediaSegmentViewmodel msVm = _selectedSegment;
                        if (msVm != null)
                        {
                            msVm.Delete();
                            MediaSegments.Remove(msVm);
                            SelectedSegment = null;
                        }
                    },
                CanExecuteDelegate = o => _selectedSegment != null
            };
            CommandNewSegment = new UICommand()
            {
                ExecuteDelegate = o =>
                    {
                        var media = LoadedMedia;
                        var msVm = new MediaSegmentViewmodel(media) { TCIn = this.TCIn, TCOut = this.TCOut, SegmentName = Common.Properties.Resources._title_NewSegment };
                        msVm.Save();
                        media.MediaSegments.Add(msVm.MediaSegment);
                        MediaSegments.Add(msVm);
                        SelectedSegment = msVm;
                        IsSegmentNameFocused = true;
                    },
            };
            CommandSetSegmentNameFocus = new UICommand()
            {
                ExecuteDelegate = o =>
                    {
                        IsSegmentNameFocused = true;
                        NotifyPropertyChanged("IsSegmentNameFocused");
                    },
            };
        }

        bool _canStop(object o)
        {
            MediaSegmentViewmodel segment = PlayWholeClip ? SelectedSegment : null;
            IMedia media = LoadedMedia;
            if (media == null) 
                return false;
            TimeSpan duration = PlayWholeClip ? media.Duration : (segment == null ? media.Duration : segment.Duration);
            return duration.Ticks >= _engine.FrameTicks;
        }

        bool _canLoad(IMedia media)
        {
            return media.FrameRate.Equals(_engine.FormatDescription.FrameRate);
        }

        private void SegmentPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            NotifyPropertyChanged("CommandSaveSegment");
        }

        private void EnginePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "PreviewPosition")
            {
                NotifyPropertyChanged("Position");
                NotifyPropertyChanged("SliderPosition");
            }
        }
    }
}
