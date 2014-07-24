using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.EnterpriseServices;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Web;
using System.Web.Services.Description;
using System.Web.UI;
using System.Web.UI.WebControls;
using Antlr.Runtime;
using Microsoft.Ajax.Utilities;
using Microsoft.SqlServer.Server;
using SolrNet.Attributes;
using SolrNet.Impl;

namespace AdminRole.Models
{
    public class SpremenljivkeSolr
    {
        public int StevilkaDogodek { get; set; }

        [SolrField("sequentialid")]
        public long Sequ { get; set; }

        [SolrField("_version_")]
        public long Version { get; set; }

        private string _exId;
        [SolrField("external_id")]
        public string ExtId
        {
            get
            {
                if (_exId == null)
                    return String.Empty;
                else
                {
                    return _exId;
                }
            }
            set { _exId = value; }
        }

        private ICollection<string> _msgT;
        [SolrField("msgtype")]
        public ICollection<string> MsgT
        {
            get
            {
                if (_msgT == null)
                {
                    return new Collection<string> {""};
                }
                else
                {
                    return _msgT;
                }
            }
            set { _msgT = value; }
        }

        private ICollection<string> _send;
        [SolrField("sender")]
        public ICollection<string> Sender
        {
            get 
            {
                if (_send == null)
                {
                    return new Collection<string> { "" };
                }
                else
                {
                    return _send;
                }
            }
            set { _send = value; }
        }

        private ICollection<string> _dest;
        [SolrField("destination")]
        public ICollection<string> Destination
        {
            get
            {
                if (_dest == null)
                {
                    return new Collection<string> { "" };
                }
                else
                {
                    return _dest;
                }
            }
            set { _dest = value; }
        }

        private ICollection<string> _custo;
        [SolrField("customer")]
        public ICollection<string> Customer
        {
            get
            {
                if (_custo == null)
                {
                    return new Collection<string> { "" };
                }
                else
                {
                    return _custo;
                }
            }
            set { _custo = value; }
        }

        private ICollection<string> _agent;
        [SolrField("agent")]
        public ICollection<string> Agent
        {
            get
            {
                if (_agent == null)
                {
                    return new Collection<string> { "" };
                }
                else
                {
                    return _agent;
                }
            }
            set { _agent = value; }
        }

        private ICollection<string> _order;
        [SolrField("order")]
        public ICollection<string> Order
        {
            get
            {
                if (_order == null)
                {
                    return new Collection<string> { "" };
                }
                else
                {
                    return _order;
                }
            }
            set { _order = value; }
        }

        private ICollection<string> _line;
        [SolrField("line")]
        public ICollection<string> Line
        {
            get
            {
                if (_line == null)
                {
                    return new Collection<string> { "" };
                }
                else
                {
                    return _line;
                }
            }
            set { _line = value; }
        }

        private ICollection<string> _vessel;
        [SolrField("vessel")]
        public ICollection<string> Vessel
        {
            get
            {
                if (_vessel == null)
                {
                    return new Collection<string>{""};
                }
                else
                {
                    return _vessel;
                }
            }
            set { _vessel = value; }
        }

        private ICollection<string> _voyage;
        [SolrField("voyage")]
        public ICollection<string> Voyage
        {
            get 
            {
                if (_voyage == null)
                {
                    return new Collection<string> { "" };
                }
                else
                {
                    return _voyage;
                }
            }
            set { _voyage = value; }
        }

        [SolrField("date")]
        public ICollection<DateTime> Date { get; set; }

        private ICollection<string> _reference;
        [SolrField("reference")]
        public ICollection<string> Reference
        {
            get
            {
                if (_reference == null)
                {
                    return new Collection<string> { "" };
                }
                else
                {
                    return _reference;
                }
            }
            set { _reference = value; }
        }

        [SolrField("exchangetimestamp")]
        public DateTime Exchangetimestamp { get; set; }

        public DateTime Exchangetimestamp2 { get; set; }

        private string _msgStat;
        [SolrField("msg_state")]
        public string MsgS
        {
            get 
            {
                if (_msgStat == null)
                {
                    return " ";
                }
                else
                {
                    return _msgStat;
                }
            }
            set { _msgStat = value; }
        }

        private ICollection<string> _inter;
        [SolrField("internal")]
        public ICollection<string> Internal 
        {
            get
            {
                if (_inter == null)
                {
                    return new Collection<string> { "" };
                }
                else
                {
                    return _inter;
                }
            }
            set { _inter = value; }
        }

        [SolrField("data")]
        public string Data { get; set; }

        [SolrField("exchangestatus")]
        public int ExStat { get; set; }

        [SolrField("breadcrumbid")]
        public string Breadcrumbid { get; set; }

    }
}