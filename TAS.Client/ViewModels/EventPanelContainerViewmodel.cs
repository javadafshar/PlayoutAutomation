﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Input;
using TAS.Client.Common;
using TAS.Common;
using TAS.Server.Common;
using TAS.Server.Interfaces;
using resources = TAS.Client.Common.Properties.Resources;


namespace TAS.Client.ViewModels
{
    public class EventPanelContainerViewmodel: EventPanelViewmodelBase
    {
        public EventPanelContainerViewmodel(IEvent ev, EventPanelViewmodelBase parent): base(ev, parent) {
            if (ev.EventType != TEventType.Container)
                throw new ApplicationException(string.Format("Invalid panel type:{0} for event type:{1}", this.GetType(), ev.EventType));
        }

        public ICommand CommandHide { get; private set; }
        public ICommand CommandShow { get; private set; }
        public ICommand CommandPaste { get { return _engineViewmodel.CommandPasteSelected; } }
        public ICommand CommandAddSubRundown { get; private set; }

        protected override void _createCommands()
        {
            CommandHide = new UICommand()
            {
                ExecuteDelegate = o => IsVisible = false,
                CanExecuteDelegate = o => _event.IsEnabled == true
            };
            CommandShow = new UICommand()
            {
                ExecuteDelegate = o => IsVisible = true,
                CanExecuteDelegate = o => _event.IsEnabled == false
            };
            CommandAddSubRundown = new UICommand()
            {
                ExecuteDelegate = _addSubRundown
            };
        }

        public override bool IsVisible
        {
            get { return _event.IsEnabled; }
            set
            {
                if (_event.IsEnabled != value)
                {
                    _event.IsEnabled = value;
                    _event.Save();
                    NotifyPropertyChanged("IsVisible");
                    if (!value)
                        IsSelected = false;
                }
            }
        }

        void _addSubRundown(object o)
        {
            IEvent newEvent = _engine.CreateEvent();
            newEvent.EventType = TEventType.Rundown;
            newEvent.EventName = resources._title_NewRundown;
            newEvent.StartType = TStartType.Manual;
            newEvent.ScheduledTime = DateTime.Now.ToUniversalTime();
            _event.InsertUnder(newEvent);
        }

        protected override void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(sender, e);
            if (e.PropertyName == "IsEnabled")
                IsVisible = _event.IsEnabled;
        }

        protected override void OnSubeventChanged(object o, CollectionOperationEventArgs<IEvent> e)
        {
            base.OnSubeventChanged(o, e);
            NotifyPropertyChanged("ChildrenCount");
        }

        public TEventType EventType { get { return TEventType.Container; } }

        public int ChildrenCount { get { return _event.SubEvents.Count; } }


    }
}
