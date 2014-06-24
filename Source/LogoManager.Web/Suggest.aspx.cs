﻿using ChannelManager.EF;
using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.UI.WebControls;

namespace ChannelManager
{
    public partial class Suggest : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {

        }

        protected void btnSubmit_Click(object sender, EventArgs e)
        {
            try
            {
                using (var ctx = new EF.RepositoryContext("LogoDB"))
                {
                    var repo = ctx.Repositorys.FirstOrDefault();

                    var suggestion = ctx.Suggestions.Create();
                    suggestion.Id = Guid.NewGuid();
                    suggestion.Created = DateTime.Now;
                    suggestion.LastModified = DateTime.Now;
                    
                    var membership = System.Web.Security.Membership.GetUser();
                    if (membership != null)
                        suggestion.User = ctx.Users.FirstOrDefault(u => u.Id == (Guid)membership.ProviderUserKey);

                    if (!string.IsNullOrEmpty(tbxSuggestionInfo.Text.Trim()))
                        suggestion.Messages.Add(new Message() { Id = Guid.NewGuid(), Created = DateTime.Now, Suggestion = suggestion, Text = tbxSuggestionInfo.Text.Trim(), User = suggestion.User });
                    repo.Suggestions.Add(suggestion);

                    Channel channel = null;
                    if (TabContainer1.ActiveTab == tabPanelNewChannel)
                    {
                        string channelName = tbxChannelName.Text.Trim();

                        if (string.IsNullOrEmpty(channelName))
                            throw new Exception("Please give the new Channel an unique name!");

                        if (ctx.Channels.Any(c => c.Name == channelName))
                            throw new Exception(string.Format("Channel '{0}' already exists!", channelName));

                        string channelWebsite = tbxChannelWebsite.Text.Trim();
                        if (!string.IsNullOrEmpty(channelWebsite) && !channelWebsite.Contains("://"))
                            channelWebsite = "http://" + channelWebsite;

                        channel = ctx.Channels.Create();
                        channel.Id = Guid.NewGuid();
                        channel.Suggestion = suggestion;
                        channel.Name = channelName;
                        channel.Website = channelWebsite;
                        channel.RegionCode = ddlChannelRegion.SelectedValue;
                        channel.Description = tbxChannelDescription.Text.Trim();
                        channel.Type = byte.Parse(rblChannelType.SelectedValue);
                        repo.Channels.Add(channel);
                    }
                    else
                    {
                        string channelName = listFoundChannels.SelectedValue;
                        if (string.IsNullOrEmpty(channelName))
                            throw new Exception("Please select an existing Channel!");
                        channel = ctx.Channels.Include("Aliases").Include("Logos").FirstOrDefault(c => c.Name == channelName);
                    }

                    foreach (ListItem newAlias in listNewAliases.Items)
                    {
                        string newAliasTrimmed = newAlias.Value.Trim();
                        if (!string.IsNullOrEmpty(newAliasTrimmed))
                        {
                            if (!channel.Aliases.Any(a => a.Name == newAliasTrimmed))
                            {
                                var alias = ctx.Aliases.Create();
                                alias.Id = Guid.NewGuid();
                                alias.Name = newAliasTrimmed;
                                alias.Created = DateTime.Now;
                                alias.Channel = channel;
                                channel.Aliases.Add(alias);
                                alias.Suggestion = suggestion;
                            }
                        }
                    }

                    if (channel.Aliases.Count == 0)
                        throw new Exception("A Channel must have at least one Alias");

                    if (uploadLogoFile.HasFile)
                    {
                        // todo : check file is PNG and has required dimensions

                        var logo = ctx.Logos.Create();
                        logo.Id = Guid.NewGuid();
                        logo.Name = tbxLogoName.Text.Trim();
                        if (string.IsNullOrEmpty(logo.Name))
                            throw new Exception("Please give the new Logo an unique name!");
                        logo.Origin = tbxLogoOrigin.Text.Trim();
                        logo.LastModified = DateTime.Now;
                        repo.Logos.Add(logo);
                        logo.Suggestion = suggestion;
                        logo.Creator = suggestion.User;
                        logo.Channels.Add(channel);
                        uploadLogoFile.SaveAs(Path.Combine(Server.MapPath("~/Logos"), logo.Id + ".png"));
                    }

                    ctx.ChangeTracker.DetectChanges();

                    if (channel.Logos.Count == 0)
                        throw new Exception("Please specify a logo for the new Channel");
                    
                    ctx.SaveChanges();

                    Response.Redirect(Request.Url.AbsoluteUri, false);
                    HttpContext.Current.ApplicationInstance.CompleteRequest();
                    this.Visible = false;
                }
            }
            catch (Exception ex)
            {
                lblReturnMessage.Visible = true;
                lblReturnMessage.Text = ex.Message;
            }
        }

        protected void btnSearch_Click(object sender, EventArgs e)
        {
            listFoundChannels.Items.Clear();
            string search = tbxChannelSearch.Text.Trim();
            using (var ctx = new EF.RepositoryContext("LogoDB"))
            {
                foreach(var channelName in ctx.Channels.Where(c => c.Suggestion == null && c.Name.Contains(search)).Select(c => c.Name))
                    listFoundChannels.Items.Add(channelName);
            }
        }

        protected void listFoundChannels_SelectedIndexChanged(object sender, EventArgs e)
        {
            panelSelectedChannelInfo.Visible = (sender as ListBox).SelectedIndex > -1;
            string channelName = (sender as ListBox).SelectedValue;
            using (var ctx = new EF.RepositoryContext("LogoDB"))
            {
                var channel = ctx.Channels.Include("Aliases").FirstOrDefault(c => c.Name == channelName);
                if (channel != null)
                {
                    lblSelectedChannelRegion.Text = channel.RegionCode;
                    linkSelectedChannel.NavigateUrl = channel.Website;
                    linkSelectedChannel.Text = channel.Website;
                    lblSelectedChannelDescription.Text = channel.Description;
                    rblSelectedChannelType.SelectedValue = channel.Type.ToString();
                    lbSelectedChannelAliases.Items.Clear();
                    foreach(var alias in channel.Aliases)
                        lbSelectedChannelAliases.Items.Add(alias.Name);
                    lbSelectedChannelAliases.Visible = lbSelectedChannelAliases.Items.Count > 0;
                }
            }
        }

        protected void btnAddAlias_Click(object sender, EventArgs e)
        {
            string newAlias = tbxChannelAliases.Text.Trim();
            if (!string.IsNullOrWhiteSpace(newAlias))
            {
                listNewAliases.Items.Add(newAlias);
                tbxChannelAliases.Text = "";
                btnRemoveAlias.Visible = true;
            }
        }

        protected void btnRemoveAlias_Click(object sender, EventArgs e)
        {
            listNewAliases.Items.Remove(listNewAliases.SelectedItem);
            btnRemoveAlias.Visible = listNewAliases.Items.Count > 0;
        }
    }
}