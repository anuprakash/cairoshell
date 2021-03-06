﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using System.Windows.Threading;
using System.Diagnostics;
using CairoDesktop.Configuration;

namespace CairoDesktop.Common
{
    public class SystemFile : INotifyPropertyChanged
    {
        private static readonly string[] ImageFileTypes = new string[] { ".jpg", ".jpeg", ".gif", ".bmp", ".png" };
        private Dispatcher _dispatcher;
        private ImageSource _icon;
        private string _friendlyName;
        private string _fullName;
        private string _name;
        private List<string> _verbs = new List<string>();

        public bool IsDirectory
        {
            get
            {
                try
                {
                    return Interop.Shell.Exists(FullName) && (File.GetAttributes(this.FullName) & FileAttributes.Directory) == FileAttributes.Directory;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Initializes a new instance of the SystemFile class using the CurrentDispatcher.
        /// </summary>
        /// <param name="filePath">The file path of the file in question.</param>
        public SystemFile(string filePath)
            : this(filePath, Dispatcher.CurrentDispatcher)
        {
        }

        /// <summary>
        /// Initializes a new instance of the SystemFile class.
        /// </summary>
        /// <param name="filePath">The file path of the file in question.</param>
        /// <param name="dispatcher">The current UI dispatcher.</param>
        public SystemFile(string filePath, Dispatcher dispatcher)
        {
            this._dispatcher = dispatcher;

            SetFilePath(filePath);
        }

        public bool SetFilePath(string filePath)
        {
            if (Interop.Shell.Exists(filePath))
            {
                this.FullName = filePath;

                getVerbs();

                return true;
            }
            return false;
        }

        /// <summary>
        /// Initializes the details of the file.
        /// </summary>
        private void getVerbs()
        {
            Process refProc = new Process();
            refProc.StartInfo.FileName = this.FullName;

            try
            {
                this.Verbs.AddRange(refProc.StartInfo.Verbs);
            }
            catch { }

            refProc.Dispose();
        }

        /// <summary>
        /// Gets or sets the name of the file.
        /// </summary>
        public string Name
        {
            get
            {
                if (_name == null)
                {
                    _name = Path.GetFileName(this.FullName);
                }

                return _name;
            }

            private set
            {
                _name = value;
                OnPropertyChanged("Name");
            }
        }

        /// <summary>
        /// Gets the name of the file, without extension depending on user preference.
        /// </summary>
        public string FriendlyName
        {
            get
            {
                if (_friendlyName == null)
                {
                    if ((Settings.ShowFileExtensions || IsDirectory) && this.Name != null)
                        this.FriendlyName = this.Name;
                    else
                        this.FriendlyName = Path.GetFileNameWithoutExtension(this.FullName);
                }

                return _friendlyName;
            }

            private set
            {
                _friendlyName = value;
                OnPropertyChanged("FriendlyName");
            }
        }

        /// <summary>
        /// Gets or sets the FullName of the System File.
        /// </summary>
        public string FullName
        {
            get
            {
                return _fullName;
            }

            set
            {
                _fullName = value;
                OnPropertyChanged("FullName");


                // reset affected properties
                _name = null;
                OnPropertyChanged("Name");

                _friendlyName = null;
                OnPropertyChanged("FriendlyName");

                _icon = null;
                OnPropertyChanged("Icon");
            }
        }

        /// <summary>
        /// Gets or sets the Icon associated with this file.
        /// This is a Dependency Property.
        /// </summary>
        public ImageSource Icon
        {
            get
            {
                if (_icon == null)
                {
                    _icon = GetDisplayIcon();
                    _icon.Freeze();
                }

                return _icon;
            }
            set
            {
                _icon = value;
                OnPropertyChanged("Icon");
            }
        }

        /// <summary>
        /// Gets the verbs for the StartInfo
        /// </summary>
        public List<string> Verbs
        {
            get
            {
                return _verbs;
            }

            private set
            {

                _verbs = value;
                OnPropertyChanged("Verbs");
            }
        }

        public static long GetFileSize(string path)
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return file.Length;
            }
        }

        /// <summary>
        /// Checks the path's file extension against the list of known types to determine if the file is an image.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns>Indication of if the file is an image or not.</returns>
        public static bool GetFileIsImage(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return false;
                }

                string ext = Path.GetExtension(path);
                foreach (string fileType in ImageFileTypes)
                {
                    if (ext.Equals(fileType, StringComparison.OrdinalIgnoreCase) && GetFileSize(path) >= 1)
                    {
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        public static bool RenameFile(string oldFilePathName, string newFilename)
        {
            try
            {
                // get the file attributes for file or directory
                FileAttributes attr = File.GetAttributes(oldFilePathName);
                string newFilePathName = Path.GetDirectoryName(oldFilePathName) + "\\" + newFilename;

                if (newFilePathName != oldFilePathName)
                {
                    //detect whether its a directory or file
                    if ((attr & FileAttributes.Directory) == FileAttributes.Directory)
                        Directory.Move(oldFilePathName, newFilePathName);
                    else
                        File.Move(oldFilePathName, newFilePathName);
                }
                return true;
            }
            catch (Exception ex)
            {
                CairoMessage.Show("The file was unable to be renamed because: " + ex.Message, "Unable to rename", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Retrieves the display icon of the file.
        /// If the file is an image then it will return the image its self (e.g. preview).
        /// </summary>
        private ImageSource GetDisplayIcon()
        {
            if (Interop.Shell.Exists(this.FullName))
            {
                if (GetFileIsImage(this.FullName))
                {
                    try
                    {
                        BitmapImage img = new BitmapImage();
                        img.BeginInit();
                        img.UriSource = new Uri(this.FullName);
                        img.CacheOption = BitmapCacheOption.OnLoad;
                        int dSize = 32;
                        Interop.Shell.TransformToPixels(32, 32, out dSize, out dSize);
                        img.DecodePixelWidth = dSize;
                        img.EndInit();
                        img.Freeze();

                        return img;
                    }
                    catch
                    {
                        return AppGrabber.IconImageConverter.GetImageFromAssociatedIcon(this.FullName);
                    }
                }
                else
                {
                    // This will attempts to get the icon via AppGrabber - if it fails the default icon will be returned.
                    return AppGrabber.IconImageConverter.GetImageFromAssociatedIcon(this.FullName);
                }
            }
            else
            {
                return AppGrabber.IconImageConverter.GetDefaultIcon();
            }
        }

        #region INotifyPropertyChanged Members

        /// <summary>
        /// This Event is raised whenever a property of this object has changed. Necesary to sync state when binding.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        [DebuggerNonUserCode]
        private void OnPropertyChanged(string propName)
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(propName));
            }
        }
        #endregion
    }

}
