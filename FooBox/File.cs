//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated from a template.
//
//     Manual changes to this file may cause unexpected behavior in your application.
//     Manual changes to this file will be overwritten if the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace FooBox
{
    using System;
    using System.Collections.Generic;
    
    public partial class File
    {
        public File()
        {
            this.ParentFolders = new HashSet<Folder>();
        }
    
        public long Id { get; set; }
        public string Name { get; set; }
        public ObjectState State { get; set; }
        public System.DateTime TimeStamp { get; set; }
    
        public virtual ICollection<Folder> ParentFolders { get; set; }
    }
}
