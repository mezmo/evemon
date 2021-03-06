using System;
using System.Collections.Generic;
using System.Linq;
using EVEMon.Common.Constants;
using EVEMon.Common.Enumerations;
using EVEMon.Common.Enumerations.CCPAPI;
using EVEMon.Common.Extensions;
using EVEMon.Common.Interfaces;
using EVEMon.Common.Serialization.Eve;
using EVEMon.Common.Service;

namespace EVEMon.Common.Models
{
    public sealed class EveMailMessage : IEveMessage
    {
        private readonly SerializableMailMessagesListItem m_source;
        private readonly CCPCharacter m_ccpCharacter;

        private IEnumerable<string> m_mailingLists;
        private IEnumerable<string> m_toCharacters;
        private string m_toCorpOrAlliance;
        private bool m_queryPending;


        #region Constructor

        /// <summary>
        /// Constructor from the API.
        /// </summary>
        /// <param name="ccpCharacter"></param>
        /// <param name="src"></param>
        internal EveMailMessage(CCPCharacter ccpCharacter, SerializableMailMessagesListItem src)
        {
            m_ccpCharacter = ccpCharacter;
            m_source = src;

            State = src.SenderID != ccpCharacter.CharacterID
                ? EveMailState.Inbox
                : EveMailState.SentItem;
            MessageID = src.MessageID;
            SentDate = src.SentDate;
            Title = src.Title.HtmlDecode();
            SenderName = src.SenderName;
            m_toCharacters = GetIDsToNames(src.ToCharacterIDs);
            m_mailingLists = GetMailingListIDsToNames(src.ToListID);
            m_toCorpOrAlliance = GetCorpOrAlliance(src.ToCorpOrAllianceID);
            EVEMailBody = new EveMailBody(new SerializableMailBodiesListItem
            {
                MessageID = 0,
                MessageText = String.Empty
            });
        }

        #endregion


        #region Properties

        /// <summary>
        /// Gets or sets the EVE mail state.
        /// </summary>
        /// <value>The state.</value>
        public EveMailState State { get; }

        /// <summary>
        /// Gets or sets the EVE mail message ID.
        /// </summary>
        /// <value>The message ID.</value>
        public long MessageID { get; }

        /// <summary>
        /// Gets or sets the EVE mail sender name.
        /// </summary>
        /// <value>The sender.</value>
        public string SenderName { get; }

        /// <summary>
        /// Gets or sets the sent date of the EVE mail.
        /// </summary>
        /// <value>The sent date.</value>
        public DateTime SentDate { get; }

        /// <summary>
        /// Gets or sets the EVE mail title.
        /// </summary>
        /// <value>The title.</value>
        public string Title { get; }

        /// <summary>
        /// Gets or sets the EVE mail recipient (corp or alliance).
        /// </summary>
        public string ToCorpOrAlliance => m_toCorpOrAlliance == EveMonConstants.UnknownText
            ? m_toCorpOrAlliance = GetCorpOrAlliance(m_source.ToCorpOrAllianceID)
            : m_toCorpOrAlliance;

        /// <summary>
        /// Gets or sets the EVE mail recipient(s) (characters).
        /// </summary>
        public IEnumerable<string> ToCharacters => m_toCharacters.Contains(EveMonConstants.UnknownText)
            ? m_toCharacters = GetIDsToNames(m_source.ToCharacterIDs)
            : m_toCharacters;

        /// <summary>
        /// Gets or sets the EVE mail recipient (mailing lists).
        /// </summary>
        public IEnumerable<string> ToMailingLists => m_mailingLists.Contains(EveMonConstants.UnknownText)
            ? m_mailingLists = GetMailingListIDsToNames(m_source.ToListID)
            : m_mailingLists;

        /// <summary>
        /// Gets the recipient.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<string> Recipient => !String.IsNullOrEmpty(ToCharacters.FirstOrDefault())
            ? ToCharacters
            : !String.IsNullOrEmpty(ToCorpOrAlliance)
                ? new List<string>
                {
                    ToCorpOrAlliance
                }
                : !String.IsNullOrEmpty(ToMailingLists.FirstOrDefault())
                    ? ToMailingLists
                    : Enumerable.Empty<string>();

        /// <summary>
        /// Gets or sets the EVE mail body.
        /// </summary>
        /// <value>The EVE mail body.</value>
        public EveMailBody EVEMailBody { get; private set; }

        /// <summary>
        /// Gets the EVE mail body text.
        /// </summary>
        /// <value>The text.</value>
        public string Text => EVEMailBody.BodyText.Normalize();

        #endregion


        #region Helper Methods

        /// <summary>
        /// Gets the sender.
        /// </summary>
        /// <param name="toCorpOrAlliance">The source.</param>
        /// <returns></returns>
        private string GetCorpOrAlliance(string toCorpOrAlliance) => toCorpOrAlliance == m_ccpCharacter.Corporation.Name
            ? m_ccpCharacter.Corporation.Name
            : toCorpOrAlliance == m_ccpCharacter.AllianceName
                ? m_ccpCharacter.AllianceName
                : EveIDToName.GetIDToName(toCorpOrAlliance);


        /// <summary>
        /// Gets the names of the IDs.
        /// </summary>
        /// <param name="src">A list of IDs.</param>
        /// <returns>A list of names</returns>
        private IEnumerable<string> GetIDsToNames(ICollection<string> src)
        {
            // If there are no IDs to query return a list with an empty entry
            if (!src.Any())
            {
                src.Add(String.Empty);
                return src;
            }

            List<string> listOfNames = new List<string>();
            List<string> listOfIDsToQuery = new List<string>();

            foreach (string id in src)
            {
                if (id == m_ccpCharacter.CharacterID.ToString(CultureConstants.InvariantCulture))
                    listOfNames.Add(m_ccpCharacter.Name);
                else
                    listOfIDsToQuery.Add(id);
            }

            // We have IDs to query
            if (listOfIDsToQuery.Any())
                listOfNames.AddRange(EveIDToName.GetIDsToNames(listOfIDsToQuery));

            return listOfNames;
        }


        /// <summary>
        /// Gets the mailing list IDs to names.
        /// </summary>
        /// <param name="mailingListIDs">The mailing list IDs.</param>
        /// <returns></returns>
        private IEnumerable<string> GetMailingListIDsToNames(ICollection<string> mailingListIDs)
        {
            // If there are no IDs to query return a list with an empty entry
            if (!mailingListIDs.Any())
            {
                mailingListIDs.Add(String.Empty);
                return mailingListIDs;
            }

            List<string> listOfNames = mailingListIDs.Select(listID => m_ccpCharacter.EVEMailingLists.FirstOrDefault(
                x => x.ID.ToString(CultureConstants.InvariantCulture) == listID)).Select(
                    mailingList => mailingList?.Name ?? EveMonConstants.UnknownText).ToList();

            // In case the list returned from the API is empty, add an "Unknown" entry
            if (!listOfNames.Any())
                listOfNames.Add(EveMonConstants.UnknownText);

            return listOfNames;
        }

        #endregion


        #region Querying

        /// <summary>
        /// Gets the EVE mail body.
        /// </summary>
        public void GetMailBody()
        {
            // Exit if we are already trying to download the mail message body text
            if (m_queryPending)
                return;

            m_queryPending = true;

            // Quits if access denied
            APIKey apiKey = m_ccpCharacter.Identity.FindAPIKeyWithAccess(CCPAPICharacterMethods.MailBodies);
            if (apiKey == null)
                return;

            EveMonClient.APIProviders.CurrentProvider.QueryMethodAsync<SerializableAPIMailBodies>(
                CCPAPICharacterMethods.MailBodies,
                apiKey.ID,
                apiKey.VerificationCode,
                m_ccpCharacter.CharacterID,
                MessageID,
                OnEVEMailBodyDownloaded);
        }

        /// <summary>
        /// Processes the queried EVE mail message mail body.
        /// </summary>
        /// <param name="result">The result.</param>
        private void OnEVEMailBodyDownloaded(CCPAPIResult<SerializableAPIMailBodies> result)
        {
            m_queryPending = false;

            // Notify an error occured
            if (m_ccpCharacter.ShouldNotifyError(result, CCPAPICharacterMethods.MailBodies))
                EveMonClient.Notifications.NotifyEVEMailBodiesError(m_ccpCharacter, result);

            // Quits if there is an error
            if (result.HasError)
                return;

            // If there is an error response on missing IDs inform the user
            if (!String.IsNullOrEmpty(result.Result.MissingMessageIDs))
            {
                result.Result.Bodies.Add(
                    new SerializableMailBodiesListItem
                    {
                        MessageID = long.Parse(result.Result.MissingMessageIDs, CultureConstants.InvariantCulture),
                        MessageText = "The text for this message was reported missing."
                    });
            }

            // Quit if for any reason there is no text
            if (!result.Result.Bodies.Any())
                return;

            // Import the data
            EVEMailBody = new EveMailBody(result.Result.Bodies.First());

            EveMonClient.OnCharacterEVEMailBodyDownloaded(m_ccpCharacter);
        }

        #endregion
    }
}