﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.Web;
using System.Web.Mvc;
using FooBox;
using FooBox.Models;
using System.Text;
using System.IO;

namespace FooBox.Controllers
{
    [Authorize]
    public class FileController : Controller
    {
        private FileManager _fileManager = new FileManager();

        public ActionResult Index()
        {
            return RedirectToAction("Browse");
        }

        private FileBrowseViewModel CreateBrowseModelForFolder(Folder folder, string fullDisplayName)
        {
            Folder userRootFolder = _fileManager.GetUserRootFolder(User.Identity.GetUserId());

            if (folder == null)
            {
                folder = userRootFolder;
                fullDisplayName = "";
            }

            FileBrowseViewModel model = new FileBrowseViewModel();

            model.FullDisplayName = fullDisplayName;
            model.DisplayName = folder == userRootFolder ? "Home" : folder.DisplayName;
            model.Files = (
                from file in folder.Files.AsQueryable()
                where file.State == ObjectState.Normal
                let latestVersion = (file is Document) ? (from version in ((Document)file).DocumentVersions.AsQueryable()
                                                          orderby version.TimeStamp descending
                                                          select version).SingleOrDefault()
                                                         : null
                select new FileBrowseViewModel.FileEntry
                {
                    FullDisplayName = fullDisplayName + "/" + file.DisplayName,
                    DisplayName = file.DisplayName,
                    IsFolder = file is Folder,
                    Size = latestVersion != null ? latestVersion.Blob.Size : 0,
                    TimeStamp = latestVersion != null ? latestVersion.TimeStamp : DateTime.UtcNow
                }
                ).ToList();
            model.Parents = new List<Tuple<string, string>>();

            List<Folder> parentFolders = new List<Folder>();

            if (folder != userRootFolder)
            {
                Folder parentFolder = folder.ParentFolder;

                while (parentFolder != userRootFolder)
                {
                    parentFolders.Add(parentFolder);
                    parentFolder = parentFolder.ParentFolder;
                }
            }

            parentFolders.Reverse();

            if (folder != userRootFolder)
                model.Parents.Add(new Tuple<string, string>("Home", ""));

            StringBuilder sb = new StringBuilder();

            foreach (var f in parentFolders)
            {
                sb.Append('/');
                sb.Append(f.DisplayName);

                model.Parents.Add(new Tuple<string, string>(f.DisplayName, sb.ToString()));
            }

            return model;
        }

        public ActionResult Browse()
        {
            string path = (string)RouteData.Values["path"] ?? "";
            string fullDisplayName = null;
            File file = _fileManager.FindFile(path, _fileManager.GetUserRootFolder(User.Identity.GetUserId()), out fullDisplayName);

            if (file == null || !(file is Folder))
            {
                ModelState.AddModelError("", "The path '" + path + "' is invalid.");
                return View(CreateBrowseModelForFolder(null, null));
            }

            return View(CreateBrowseModelForFolder((Folder)file, fullDisplayName));
        }


        private void UploadBlob(Client client, Stream stream, out string hash, out long size)
        {
            var clientUploadDirectory = _fileManager.AccessClientUploadDirectory(client.Id);
            var randomName = Utilities.GenerateRandomString(FileManager.IdChars, 32);
            var tempUploadFileName = clientUploadDirectory.FullName + "\\" + randomName;

            byte[] buffer = new byte[4096 * 4];
            int bytesRead;
            long totalBytesRead = 0;

            using (var hashAlgorithm = _fileManager.CreateBlobHashAlgorithm())
            {
                using (var fileStream = new FileStream(tempUploadFileName, FileMode.Create))
                {
                    while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        hashAlgorithm.TransformBlock(buffer, 0, bytesRead, null, 0);
                        fileStream.Write(buffer, 0, bytesRead);
                        totalBytesRead += bytesRead;
                    }
                }

                hashAlgorithm.TransformFinalBlock(new byte[0], 0, 0);
                hash = (new SoapHexBinary(hashAlgorithm.Hash)).ToString();

                try
                {
                    System.IO.File.Move(tempUploadFileName, clientUploadDirectory.FullName + "\\" + hash);
                }
                catch
                {
                    // We're going to assume that the file with hash as its name already exists.
                    // This means that someone has already uploaded an identical file.
                    System.IO.File.Delete(tempUploadFileName);
                }
            }

            size = totalBytesRead;
        }

        [HttpPost]
        public ActionResult Upload(string fromPath, HttpPostedFileBase uploadFile)
        {
            long userId = User.Identity.GetUserId();
            var internalClient = _fileManager.GetInternalClient(userId);
            string fullDisplayName = null;
            File file = _fileManager.FindFile(fromPath ?? "", _fileManager.GetUserRootFolder(userId), out fullDisplayName);

            if (file == null || !(file is Folder))
                return RedirectToAction("Browse");

            string hash;
            long fileSize;

            UploadBlob(internalClient, uploadFile.InputStream, out hash, out fileSize);

            ClientSyncData data = new ClientSyncData();

            data.ClientId = internalClient.Id;
            data.BaseChangelistId = _fileManager.GetLastChangelistId();
            data.Changes.Add(new ClientChange
            {
                FullName = "/" + userId + "/" + fromPath + "/" + uploadFile.FileName,
                Type = ChangeType.Add,
                IsFolder = false,
                Size = fileSize,
                Hash = hash,
                DisplayName = uploadFile.FileName
            });

            _fileManager.SyncClientChanges(data);

            return RedirectToAction("Browse", new { path = fromPath });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _fileManager.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
