cyclonedds\src\core\ddsc\include\dds\dds.h
-------------------------------------------
...
#include "dds/ddsrt/time.h"
#include "dds/ddsrt/retcode.h"
#include "dds/ddsrt/log.h"
#include "dds/ddsrt/iovec.h" // <============ added
#include "dds/ddsc/dds_public_impl.h"
#include "dds/ddsc/dds_public_alloc.h"
#include "dds/ddsc/dds_public_qos.h"
...



cyclonedds\src/core/ddsc/src/dds_topic.c
------------------------------


/**
 * @brief Get the sertype associated with a topic.
 * @ingroup topic
 *
 * @param[in] topic The topic entity.
 * @returns The sertype pointer, or NULL if the entity is not a topic or invalid.
 */
DDS_EXPORT const struct ddsi_sertype * dds_get_topic_sertype (dds_entity_t topic)
{
  struct dds_topic *tp;
  if (dds_topic_pin (topic, &tp) != DDS_RETCODE_OK)
    return NULL;
  const struct ddsi_sertype *st = tp->m_stype;
  dds_topic_unpin (tp);
  return st;
}

#include "dds/ddsi/ddsi_serdata.h"

DDS_EXPORT struct ddsi_serdata *dds_serdata_ref(struct ddsi_serdata *serdata) {
    return ddsi_serdata_ref(serdata);
}

DDS_EXPORT void dds_serdata_unref(struct ddsi_serdata *serdata) {
    ddsi_serdata_unref(serdata);
}

DDS_EXPORT uint32_t dds_serdata_size(const struct ddsi_serdata *serdata) {
    return ddsi_serdata_size(serdata);
}

DDS_EXPORT void dds_serdata_to_ser(const struct ddsi_serdata *serdata, size_t off, size_t sz, void *buf) {
    ddsi_serdata_to_ser(serdata, off, sz, buf);
}

DDS_EXPORT struct ddsi_serdata *dds_serdata_from_ser_iov(const struct ddsi_sertype *type, int kind, uint32_t niov, const ddsrt_iovec_t *iov, size_t size) {
    return ddsi_serdata_from_ser_iov(type, (enum ddsi_serdata_kind)kind, niov, iov, size);
}

DDS_EXPORT uint32_t dds_sample_info_size(void) {
    return (uint32_t)sizeof(dds_sample_info_t);
}






cyclonedds\src/core/ddsc/src/dds_write.c
------------------------------------------


dds_return_t dds_writecdr (dds_entity_t writer, struct ddsi_serdata *serdata)
{
  //printf("[native] dds_writecdr called for writer 0x%x, serdata 0x%p\n", writer, serdata);
  dds_return_t ret;
  dds_writer *wr;

  if (serdata == NULL)
    return DDS_RETCODE_BAD_PARAMETER;

  if ((ret = dds_writer_lock (writer, &wr)) != DDS_RETCODE_OK) {
    ddsi_serdata_unref(serdata); // <======== ADDED
    return ret;
  }
  if (wr->m_topic->m_filter.mode != DDS_TOPIC_FILTER_NONE)
  {
    dds_writer_unlock (wr);
    ddsi_serdata_unref(serdata); // <======== ADDED
    return DDS_RETCODE_ERROR;
  }
  serdata->statusinfo = 0;
  serdata->timestamp.v = dds_time ();
  ret = dds_writecdr_impl (wr, wr->m_xp, serdata, !wr->whc_batch);
  dds_writer_unlock (wr);
  //printf("[native] dds_writecdr returned %d\n", ret);
  return ret;
}

