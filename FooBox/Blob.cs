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
    
    public partial class Blob
    {
        public Blob()
        {
            this.Documents = new HashSet<Document>();
        }
    
        public System.Guid Id { get; set; }
        public long Size { get; set; }
        public string Hash { get; set; }
    
        public virtual ICollection<Document> Documents { get; set; }
    }
}
